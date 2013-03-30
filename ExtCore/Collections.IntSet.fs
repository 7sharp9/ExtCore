﻿(*

Copyright 2013 Jack Pappas

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

*)

//
namespace ExtCore.Collections

open System.Collections.Generic
open System.Diagnostics
open LanguagePrimitives
open OptimizedClosures
open ExtCore


/// Bitwise operations necessary for implementing IntMap.
module internal BitOps =
    /// <summary>Determines if all specified bits are cleared (not set) in a value.</summary>
    /// <param name="value">The value to test.</param>
    /// <param name="bitValue">The bits to test in 'value'.</param>
    /// <returns>true if all bits which are set in 'bitValue' are *not* set in 'value'.</returns>
    let inline zeroBit (value : ^T, bitValue) : bool =
        value &&& bitValue = GenericZero

    //
    let inline mask (value : ^T, bitValue) =
        (value ||| (bitValue - GenericOne)) &&& ~~~bitValue

    //
    let inline matchPrefix (k : ^T, p, m) =
        mask (k, m) = p

//    //
//    let inline leastSignificantSetBit (x : uint32) : uint32 =
//        x &&& (uint32 -(int x))

    // http://aggregate.org/MAGIC/#Most%20Significant%201%20Bit
    // OPTIMIZE : This could be even faster if we could take advantage of a built-in
    // CPU instruction here (such as 'bsr', 'ffs', or 'clz').
    // Could we expose these instructions through Mono?
    let inline private mostSignificantSetBit (x : uint32) =
        let x = x ||| (x >>> 1)
        let x = x ||| (x >>> 2)
        let x = x ||| (x >>> 4)
        let x = x ||| (x >>> 8)
        let x = x ||| (x >>> 16)
        //x &&& ~~~(x >>> 1)
        // OPTIMIZATION : p AND (NOT q) <-> p XOR q
        x ^^^ (x >>> 1)

    /// Finds the first bit at which p0 and p1 disagree.
    /// Returns a power-of-two value containing this (and only this) bit.
    let inline branchingBit (p0, p1) : uint32 =
        mostSignificantSetBit (p0 ^^^ p1)


/// Constants used to tune certain operations on Patricia tries.
module internal PatriciaTrieConstants =
    /// The default capacity used to when creating mutable stacks
    /// within the optimized traversal methods for Patricia tries.
    // TODO : Run some experiments to determine if this is a good initial capacity,
    // or if some other value would provide better average-case performance.
    let [<Literal>] defaultTraversalStackSize = 64

open BitOps
open PatriciaTrieConstants


/// A Patricia trie implementation.
/// Used as the underlying data structure for IntSet (and TagSet).
type internal PatriciaSet =
    | Empty
    // Key
    | Lf of uint32
    // Prefix * Mask * Left-Child * Right-Child
    | Br of uint32 * uint32 * PatriciaSet * PatriciaSet

    //
    static member Contains (key, set : PatriciaSet) =
        match set with
        | Empty ->
            false
        | (Lf j) ->
            key = j
        | Br (_, m, t0, t1) ->
            PatriciaSet.Contains
                (key, (if zeroBit (key, m) then t0 else t1))

    //
    static member Count (set : PatriciaSet) : int =
        match set with
        | Empty -> 0
        | Lf _ -> 1
        | Br (_,_,_,_) as t ->
            /// The stack of trie nodes pending traversal.
            let stack = Stack (defaultTraversalStackSize)

            // Add the initial tree to the stack.
            stack.Push t

            // Traverse the tree, counting the elements by using the mutable stack.
            let mutable count = 0u

            while stack.Count <> 0 do
                match stack.Pop () with
                | Empty -> ()
                | Lf _ ->
                    count <- count + 1u
                    
                (* OPTIMIZATION :   When one or both children of this node are leaves,
                                    we handle them directly since it's a little faster. *)
                | Br (_, _, Lf _, Lf _) ->
                    count <- count + 2u
                    
                | Br (_, _, Lf _, child)
                | Br (_, _, child, Lf _) ->
                    count <- count + 1u
                    stack.Push child

                | Br (_, _, left, right) ->
                    // Push both children onto the stack and recurse to process them.
                    // NOTE : They're pushed in the opposite order we want to visit them!
                    stack.Push right
                    stack.Push left

            // Return the computed element count.
            int count

    //
    static member Remove (key, set : PatriciaSet) =
        match set with
        | Empty ->
            Empty
        | (Lf j) as t ->
            if j = key then Empty
            else t
        
        | Br (p, m, t0, t1) as t ->
            if matchPrefix (key, p, m) then
                if zeroBit (key, m) then
                    match PatriciaSet.Remove (key, t0) with
                    | Empty -> t1
                    | t0 ->
                        Br (p, m, t0, t1)
                else
                    match PatriciaSet.Remove (key, t1) with
                    | Empty -> t0
                    | t1 ->
                        Br (p, m, t0, t1)
            else t

    //
    static member (*inline*) private Join (p0, t0 : PatriciaSet, p1, t1) =
        let m = branchingBit (p0, p1)
        let p = mask (p0, m)
        if zeroBit (p0, m) then
            Br (p, m, t0, t1)
        else
            Br (p, m, t1, t0)

    //
    static member Insert (key, set) =
        // TODO : Lift this recursive function out into a private member
        // Or, better yet -- with a few tweaks, we could just re-use this member and call it recursively.
        let rec ins = function
            | Empty ->
                Lf key
            | (Lf j) as t ->
                let newLeaf = Lf key
                if j = key then
                    newLeaf
                else
                    PatriciaSet.Join (key, newLeaf, j, t)

            | Br (p, m, t0, t1) as t ->
                if matchPrefix (key, p, m) then
                    if zeroBit (key, m) then
                        let left = ins t0
                        Br (p, m, left, t1)
                    else
                        let right = ins t1
                        Br (p, m, t0, right)
                else
                    PatriciaSet.Join (key, Lf key, p, t)

        // Call the implementation function.
        ins set

    //
    static member Merge (s, t) : PatriciaSet =
        match s, t with
        | Empty, t
        | t, Empty ->
            t
        | Lf k, t ->
            PatriciaSet.Insert (k, t)
        | (Br (p, m, s0, s1) as s), Lf k ->
            if matchPrefix (k, p, m) then
                if zeroBit (k, m) then
                    let left = PatriciaSet.Insert (k, s0)
                    Br (p, m, left, s1)
                else
                    let right = PatriciaSet.Insert (k, s1)
                    Br (p, m, s0, right)
            else
                PatriciaSet.Join (k, Lf k, p, s)

        | (Br (p, m, s0, s1) as s), (Br (q, n, t0, t1) as r) ->
            if m = n && p = q then
                // The trees have the same prefix. Merge the subtrees.
                let left = PatriciaSet.Merge (s0, t0)
                let right = PatriciaSet.Merge (s1, t1)
                Br (p, m, left, right)
                
            elif m < n && matchPrefix (q, p, m) then
                // q contains p. Merge t with a subtree of s.
                if zeroBit (q, m) then
                    let left = PatriciaSet.Merge (s0, t)
                    Br (p, m, left, s1)
                else
                    let right = PatriciaSet.Merge (s1, t)
                    Br (p, m, s0, right)

            elif m > n && matchPrefix (p, q, n) then
                // p contains q. Merge s with a subtree of t.
                if zeroBit (p, n) then
                    let left = PatriciaSet.Merge (s, t0)
                    Br (q, n, left, t1)
                else
                    let right = PatriciaSet.Merge (s, t1)
                    Br (q, n, t0, right)
            else
                // The prefixes disagree.
                PatriciaSet.Join (p, s, q, t)

    //
    static member OfSeq (source : seq<int>) : PatriciaSet =
        (Empty, source)
        ||> Seq.fold (fun trie el ->
            PatriciaSet.Insert (uint32 el, trie))

    //
    static member OfList (source : int list) : PatriciaSet =
        // Preconditions
        checkNonNull "source" source

        (Empty, source)
        ||> List.fold (fun trie el ->
            PatriciaSet.Insert (uint32 el, trie))

    //
    static member OfArray (source : int[]) : PatriciaSet =
        // Preconditions
        checkNonNull "source" source

        (Empty, source)
        ||> Array.fold (fun trie el ->
            PatriciaSet.Insert (uint32 el, trie))

    //
    static member OfSet (source : Set<int>) : PatriciaSet =
        // Preconditions
        checkNonNull "source" source

        (Empty, source)
        ||> Set.fold (fun trie el ->
            PatriciaSet.Insert (uint32 el, trie))

    //
    member this.Iterate (action : int -> unit) : unit =
        match this with
        | Empty -> ()
        | Lf k ->
            action (int k)
        | Br (_,_,_,_) as t ->
            /// The stack of trie nodes pending traversal.
            let stack = Stack (defaultTraversalStackSize)

            // Add the initial tree to the stack.
            stack.Push t

            // Loop until we've processed the entire tree.
            while stack.Count <> 0 do
                match stack.Pop () with
                | Empty -> ()
                | Lf k ->
                    action (int k)
                    
                (* OPTIMIZATION :   When one or both children of this node are leaves,
                                    we handle them directly since it's a little faster. *)
                | Br (_, _, Lf k, Lf j) ->
                    action (int k)
                    action (int j)
                    
                | Br (_, _, Lf k, right) ->
                    // Only handle the case where the left child is a leaf
                    // -- otherwise the traversal order would be altered.
                    action (int k)
                    stack.Push right

                | Br (_, _, left, right) ->
                    // Push both children onto the stack and recurse to process them.
                    // NOTE : They're pushed in the opposite order we want to visit them!
                    stack.Push right
                    stack.Push left

    //
    member this.IterateBack (action : int -> unit) : unit =
        match this with
        | Empty -> ()
        | Lf k ->
            action (int k)
        | Br (_,_,_,_) as t ->
            /// The stack of trie nodes pending traversal.
            let stack = Stack (defaultTraversalStackSize)

            // Add the initial tree to the stack.
            stack.Push t

            // Loop until we've processed the entire tree.
            while stack.Count <> 0 do
                match stack.Pop () with
                | Empty -> ()
                | Lf k ->
                    action (int k)
                    
                (* OPTIMIZATION :   When one or both children of this node are leaves,
                                    we handle them directly since it's a little faster. *)
                | Br (_, _, Lf k, Lf j) ->
                    action (int j)
                    action (int k)
                    
                | Br (_, _, left, Lf k) ->
                    // Only handle the case where the right child is a leaf
                    // -- otherwise the traversal order would be altered.
                    action (int k)
                    stack.Push left

                | Br (_, _, left, right) ->
                    // Push both children onto the stack and recurse to process them.
                    // NOTE : They're pushed in the opposite order we want to visit them!
                    stack.Push left
                    stack.Push right

    //
    member this.Fold (folder : 'State -> int -> 'State, state : 'State) : 'State =
        match this with
        | Empty ->
            state
        | Lf k ->
            folder state (int k)
        | Br (_,_,_,_) as t ->
            let folder = FSharpFunc<_,_,_>.Adapt folder

            /// The stack of trie nodes pending traversal.
            let stack = Stack (defaultTraversalStackSize)

            // Add the initial tree to the stack.
            stack.Push t

            /// Loop until we've processed the entire tree.
            let mutable state = state
            while stack.Count <> 0 do
                match stack.Pop () with
                | Empty -> ()
                | Lf k ->
                    state <- folder.Invoke (state, int k)
                    
                (* OPTIMIZATION :   When one or both children of this node are leaves,
                                    we handle them directly since it's a little faster. *)
                | Br (_, _, Lf k, Lf j) ->
                    state <- folder.Invoke (state, int k)
                    state <- folder.Invoke (state, int j)
                    
                | Br (_, _, Lf k, right) ->
                    // Only handle the case where the left child is a leaf
                    // -- otherwise the traversal order would be altered.
                    state <- folder.Invoke (state, int k)
                    stack.Push right

                | Br (_, _, left, right) ->
                    // Push both children onto the stack and recurse to process them.
                    // NOTE : They're pushed in the opposite order we want to visit them!
                    stack.Push right
                    stack.Push left

            // Return the final state value.
            state

    //
    member this.FoldBack (folder : int -> 'State -> 'State, state : 'State) : 'State =
        match this with
        | Empty ->
            state
        | Lf k ->
            folder (int k) state
        | Br (_,_,_,_) as t ->
            let folder = FSharpFunc<_,_,_>.Adapt folder

            /// The stack of trie nodes pending traversal.
            let stack = Stack (defaultTraversalStackSize)

            // Add the initial tree to the stack.
            stack.Push t

            /// Loop until we've processed the entire tree.
            let mutable state = state
            while stack.Count <> 0 do
                match stack.Pop () with
                | Empty -> ()
                | Lf k ->
                    state <- folder.Invoke (int k, state)
                    
                (* OPTIMIZATION :   When one or both children of this node are leaves,
                                    we handle them directly since it's a little faster. *)
                | Br (_, _, Lf k, Lf j) ->
                    state <- folder.Invoke (int j, state)
                    state <- folder.Invoke (int k, state)
                    
                | Br (_, _, left, Lf k) ->
                    // Only handle the case where the right child is a leaf
                    // -- otherwise the traversal order would be altered.
                    state <- folder.Invoke (int k, state)
                    stack.Push left

                | Br (_, _, left, right) ->
                    // Push both children onto the stack and recurse to process them.
                    // NOTE : They're pushed in the opposite order we want to visit them!
                    stack.Push left
                    stack.Push right

            // Return the final state value.
            state

    //
    member this.TryPick (picker : int -> 'T option) : 'T option =
        match this with
        | Empty ->
            None
        | Lf k ->
            picker (int k)
        | Br (_,_,_,_) as t ->
            /// The stack of trie nodes pending traversal.
            let stack = Stack (defaultTraversalStackSize)

            // Add the initial tree to the stack.
            stack.Push t

            /// Loop until we find a key/value that matches the picker,
            /// or until we've processed the entire tree.
            let mutable pickedValue = None
            while stack.Count <> 0 && Option.isNone pickedValue do
                match stack.Pop () with
                | Empty -> ()
                | Lf k ->
                    pickedValue <- picker <| int k
                    
                (* OPTIMIZATION :   When one or both children of this node are leaves,
                                    we handle them directly since it's a little faster. *)
                | Br (_, _, Lf k, Lf j) ->
                    pickedValue <-
                        match picker (int k) with
                        | (Some _) as value ->
                            value
                        | None ->
                            picker (int j)
                    
                | Br (_, _, Lf k, right) ->
                    // Only handle the case where the left child is a leaf
                    // -- otherwise the traversal order would be altered.
                    pickedValue <- picker (int k)
                    stack.Push right

                | Br (_, _, left, right) ->
                    // Push both children onto the stack and recurse to process them.
                    // NOTE : They're pushed in the opposite order we want to visit them!
                    stack.Push right
                    stack.Push left

            // Return the picked value.
            pickedValue

    //
    member this.ToSeq () =
        seq {
        match this with
        | Empty -> ()
        | Lf k ->
            yield int k
        
        (* OPTIMIZATION :   When one or both children of this node are leaves,
                            we handle them directly since it's a little faster. *)
        | Br (_, _, Lf k, Lf j) ->
            yield int k
            yield int j
                    
        | Br (_, _, Lf k, right) ->
            // Only handle the case where the left child is a leaf
            // -- otherwise the traversal order would be altered.
            yield int k
            yield! right.ToSeq ()

        | Br (_, _, left, right) ->
            // Recursively visit the children.
            yield! left.ToSeq ()
            yield! right.ToSeq ()
        }

//
[<Sealed>]
[<DebuggerTypeProxy(typedefof<IntSetDebuggerProxy>)>]
[<DebuggerDisplay("Count = {Count}")>]
type IntSet private (trie : PatriciaSet) =
    /// The empty IntSet.
    static member Empty
        with get () : IntSet =
            IntSet Empty

    //
    member private __.Trie
        with get () = trie

    /// The number of elements in the IntSet.
    member __.Count
        with get () : int =
            PatriciaSet.Count trie

    /// Is the map empty?
    member __.IsEmpty
        with get () =
            match trie with
            | Empty -> true
            | _ -> false

    /// Tests if an element is in the domain of the IntSet.
    member __.Contains (key : int) : bool =
        PatriciaSet.Contains (uint32 key, trie)

    /// Returns a new IntSet with the element added to this IntSet.
    member __.Add (key : int) : IntSet =
        IntSet (
            PatriciaSet.Insert (uint32 key, trie))

    /// Removes an element from the domain of the IntSet.
    /// No exception is raised if the element is not present.
    member __.Remove (key : int) : IntSet =
        IntSet (
            PatriciaSet.Remove (uint32 key, trie))

    /// Computes the union of two IntSets.
    member __.Union (otherSet : IntSet) : IntSet =
        IntSet (
            PatriciaSet.Merge (trie, otherSet.Trie))

    /// The IntSet containing the given element.
    static member Singleton (element : int) : IntSet =
        IntSet (Lf <| uint32 element)

    /// Returns a new IntSet made from the given elements.
    static member OfSeq (source : seq<int>) : IntSet =
        // Preconditions
        checkNonNull "source" source

        IntSet (PatriciaSet.OfSeq source)

    /// Returns a new IntSet made from the given elements.
    static member OfList (source : int list) : IntSet =
        // Preconditions
        checkNonNull "source" source

        // OPTIMIZATION : If the source is empty return immediately.
        if List.isEmpty source then
            IntSet.Empty
        else
            IntSet (PatriciaSet.OfList source)

    /// Returns a new IntSet made from the given elements.
    static member OfArray (source : int[]) : IntSet =
        // Preconditions
        checkNonNull "source" source

        // OPTIMIZATION : If the source is empty return immediately.
        if Array.isEmpty source then
            IntSet.Empty
        else
            IntSet (PatriciaSet.OfArray source)

    /// Returns a new IntSet made from the given elements.
    static member OfSet (source : Set<int>) : IntSet =
        // Preconditions
        checkNonNull "source" source

        // OPTIMIZATION : If the source is empty return immediately.
        if Set.isEmpty source then
            IntSet.Empty
        else
            IntSet (PatriciaSet.OfSet source)

    //
    member __.ToArray () : int[] =
        let elements = ResizeArray ()
        trie.Iterate elements.Add
        elements.ToArray ()

    //
    member __.ToList () : int list =
        trie.FoldBack ((fun el list -> el :: list), [])

    //
    member __.ToSet () : Set<int> =
        trie.FoldBack (Set.add, Set.empty)

    //
    member __.Iterate (action : int -> unit) : unit =
        trie.Iterate action

    //
    member __.IterateBack (action : int -> unit) : unit =
        trie.IterateBack action

    //
    member __.Fold (folder : 'State -> int -> 'State, state : 'State) : 'State =
        trie.Fold (folder, state)

    //
    member __.FoldBack (folder : int -> 'State -> 'State, state : 'State) : 'State =
        trie.FoldBack (folder, state)

    //
    member __.TryPick (picker : int -> 'T option) : 'T option =
        trie.TryPick picker

    //
    member this.Pick (picker : int -> 'T option) : 'T =
        match this.TryPick picker with
        | Some value ->
            value
        | None ->
            raise <| System.Collections.Generic.KeyNotFoundException ()

    //
    member this.Exists (predicate : int -> bool) : bool =
        this.TryPick (fun el ->
            if predicate el then Some () else None)
        |> Option.isSome

    //
    member this.Forall (predicate : int -> bool) : bool =
        this.TryPick (fun el ->
            if predicate el then Some () else None)
        |> Option.isNone

    //
    member __.ToSeq () =
        trie.ToSeq ()

    //
    member this.Choose (chooser : int -> int option) : IntSet =
        this.Fold ((fun chosenSet el ->
            match chooser el with
            | None ->
                chosenSet
            | Some newValue ->
                chosenSet.Add newValue), IntSet.Empty)

    //
    member this.Filter (predicate : int -> bool) : IntSet =
        this.Fold ((fun filteredSet el ->
            if predicate el then
                filteredSet
            else
                filteredSet.Remove el), this)

    //
    member this.Map (mapping : int -> int) : IntSet =
        this.Fold ((fun map el ->
            map.Add (mapping el)), IntSet.Empty)

    (* OPTIMIZE : The methods below should be replaced with optimized implementations where possible. *)

    //
    member this.Partition (predicate : int -> bool) : IntSet * IntSet =
        this.Fold ((fun (trueSet, falseSet) el ->
            if predicate el then
                trueSet.Add el,
                falseSet
            else
                trueSet,
                falseSet.Add el), (IntSet.Empty, IntSet.Empty))

    interface System.Collections.IEnumerable with
        /// <inherit />
        member __.GetEnumerator () =
            (trie.ToSeq ()).GetEnumerator ()
            :> System.Collections.IEnumerator

    interface IEnumerable<int> with
        /// <inherit />
        member __.GetEnumerator () =
            (trie.ToSeq ()).GetEnumerator ()

    interface ICollection<int> with
        /// <inherit />
        member __.Count
            with get () =
                PatriciaSet.Count trie

        /// <inherit />
        member __.IsReadOnly
            with get () = true

        /// <inherit />
        member __.Add x =
            notSupported "IntSets cannot be mutated."

        /// <inherit />
        member __.Clear () =
            notSupported "IntSets cannot be mutated."

        /// <inherit />
        member __.Contains (item : int) =
            notImpl "ICollection`1.Contains"
//            match PatriciaSet.TryFind (uint32 item.Key, trie) with
//            | None ->
//                false
//            | Some value ->
//                LanguagePrimitives.HashCompare.GenericEqualityIntrinsic<'T>(value, item.Value)

        /// <inherit />
        member this.CopyTo (array, arrayIndex) =
            // Preconditions
            checkNonNull "array" array
            if arrayIndex < 0 then
                raise <| System.ArgumentOutOfRangeException "arrayIndex"

            let count = PatriciaSet.Count trie
            if arrayIndex + count > Array.length array then
                invalidArg "arrayIndex"
                    "There is not enough room in the array to copy the \
                     elements when starting at the specified index."

            this.Fold ((fun index el ->
                array.[index] <- el
                index + 1), arrayIndex)
            |> ignore

        /// <inherit />
        member __.Remove item : bool =
            notSupported "IntSets cannot be mutated."

//
and [<Sealed>]
    internal IntSetDebuggerProxy (set : IntSet) =

    [<DebuggerBrowsable(DebuggerBrowsableState.RootHidden)>]
    member __.Items
        with get () : int[] =
            set.ToArray ()


/// Functional programming operators related to the IntSet type.
[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module IntSet =
    /// The empty IntSet.
    [<CompiledName("Empty")>]
    let empty = IntSet.Empty

    /// Is the map empty?
    [<CompiledName("IsEmpty")>]
    let inline isEmpty (set : IntSet) : bool =
        // Preconditions
        checkNonNull "set" set

        set.IsEmpty

    /// Returns the number of elements in the IntSet.
    [<CompiledName("Count")>]
    let inline count (set : IntSet) : int =
        // Preconditions
        checkNonNull "set" set
        
        set.Count

    /// The IntSet containing the given element.
    [<CompiledName("Singleton")>]
    let inline singleton (key : int) : IntSet =
        IntSet.Singleton key

    //
    [<CompiledName("Add")>]
    let inline add (key : int) (set : IntSet) : IntSet =
        // Preconditions
        checkNonNull "set" set

        set.Add key

    //
    [<CompiledName("Remove")>]
    let inline remove (key : int) (set : IntSet) : IntSet =
        // Preconditions
        checkNonNull "set" set

        set.Remove key

    //
    [<CompiledName("Contains")>]
    let inline contains (key : int) (set : IntSet) : bool =
        // Preconditions
        checkNonNull "set" set

        set.Contains key

    //
    [<CompiledName("Union")>]
    let inline union (set1 : IntSet) (set2 : IntSet) : IntSet =
        // Preconditions
        checkNonNull "set1" set1
        checkNonNull "set2" set2

        set1.Union set2

    //
    [<CompiledName("OfSeq")>]
    let inline ofSeq source : IntSet =
        // Preconditions are checked by the member.
        IntSet.OfSeq source

    //
    [<CompiledName("OfList")>]
    let inline ofList source : IntSet =
        // Preconditions are checked by the member.
        IntSet.OfList source

    //
    [<CompiledName("OfArray")>]
    let inline ofArray source : IntSet =
        // Preconditions are checked by the member.
        IntSet.OfArray source

    //
    [<CompiledName("OfSet")>]
    let inline ofSet source : IntSet =
        // Preconditions are checked by the member.
        IntSet.OfSet source

    //
    [<CompiledName("ToSeq")>]
    let inline toSeq (set : IntSet) =
        // Preconditions
        checkNonNull "set" set

        set.ToSeq ()

    //
    [<CompiledName("ToList")>]
    let inline toList (set : IntSet) =
        // Preconditions
        checkNonNull "set" set

        set.ToList ()

    //
    [<CompiledName("ToArray")>]
    let inline toArray (set : IntSet) =
        // Preconditions
        checkNonNull "set" set

        set.ToArray ()

    //
    [<CompiledName("ToSet")>]
    let inline toSet (set : IntSet) =
        // Preconditions
        checkNonNull "set" set

        set.ToSet ()

    //
    [<CompiledName("Iter")>]
    let inline iter (action : int -> unit) (set : IntSet) =
        // Preconditions
        checkNonNull "set" set

        set.Iterate action

    //
    [<CompiledName("IterBack")>]
    let inline iterBack (action : int -> unit) (set : IntSet) =
        // Preconditions
        checkNonNull "set" set

        set.IterateBack action

    //
    [<CompiledName("Fold")>]
    let inline fold (folder : 'State -> int -> 'State) (state : 'State) (set : IntSet) =
        // Preconditions
        checkNonNull "set" set

        set.Fold (folder, state)

    //
    [<CompiledName("FoldBack")>]
    let inline foldBack (folder : int -> 'State -> 'State) (set : IntSet) (state : 'State) =
        // Preconditions
        checkNonNull "set" set

        set.FoldBack (folder, state)

    //
    [<CompiledName("Choose")>]
    let inline choose (chooser : int -> int option) (set : IntSet) : IntSet =
        // Preconditions
        checkNonNull "set" set

        set.Choose chooser

    //
    [<CompiledName("Filter")>]
    let inline filter (predicate : int -> bool) (set : IntSet) : IntSet =
        // Preconditions
        checkNonNull "set" set

        set.Filter predicate

    //
    [<CompiledName("Map")>]
    let inline map (mapping : int -> int) (set : IntSet) : IntSet =
        // Preconditions
        checkNonNull "set" set

        set.Map mapping

    //
    [<CompiledName("Partition")>]
    let inline partition (predicate : int -> bool) (set : IntSet) : IntSet * IntSet =
        // Preconditions
        checkNonNull "set" set

        set.Partition predicate

    //
    [<CompiledName("Exists")>]
    let inline exists (predicate : int -> bool) (set : IntSet) : bool =
        // Preconditions
        checkNonNull "set" set

        set.Exists predicate

    //
    [<CompiledName("Forall")>]
    let inline forall (predicate : int -> bool) (set : IntSet) : bool =
        // Preconditions
        checkNonNull "set" set

        set.Forall predicate

    //
    [<CompiledName("TryPick")>]
    let inline tryPick (picker : int -> 'T option) (set : IntSet) : 'T option =
        // Preconditions
        checkNonNull "set" set

        set.TryPick picker

    //
    [<CompiledName("Pick")>]
    let inline pick (picker : int -> 'T option) (set : IntSet) : 'T =
        // Preconditions
        checkNonNull "set" set

        set.Pick picker


#if PROTO_COMPILER

/// An IntSet whose values are tagged with a unit-of-measure type.
[<MeasureAnnotatedAbbreviation>]
type TagSet<[<Measure>] 'Tag> = IntSet

/// Functional programming operators related to the TagMap<_> type.
[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TagSet =
    /// Retypes a value without emitting any IL instructions.
    /// WARNING: This should be used with EXTREME CAUTION.
    [<NoDynamicInvocation>]
    [<CompiledName("RetypeInlined")>]
    let inline private retype<'T,'U> (x:'T) : 'U = (# "" x : 'U #)

    /// The empty TagSet.
    [<CompiledName("Empty")>]
    let empty<[<Measure>] 'Tag> : TagSet<'Tag> =
        retype IntSet.Empty

    /// Is the map empty?
    [<CompiledName("IsEmpty")>]
    let inline isEmpty (set : TagSet<'Tag>) : bool =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.IsEmpty

    /// Returns the number of elements in the TagSet.
    [<CompiledName("Count")>]
    let inline count (set : TagSet<'Tag>) : int =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set
        
        set.Count

    /// The TagSet containing the given element.
    [<CompiledName("Singleton")>]
    let inline singleton (element : int<'Tag>) : TagSet<'Tag> =
        IntSet.Singleton (int element)
        |> retype

    //
    [<CompiledName("Add")>]
    let inline add (key : int<'Tag>) (set : TagSet<'Tag>) : TagSet<'Tag> =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.Add (int key)
        |> retype

    //
    [<CompiledName("Remove")>]
    let inline remove (key : int<'Tag>) (set : TagSet<'Tag>) : TagSet<'Tag> =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.Remove (int key)
        |> retype

    //
    [<CompiledName("Contains")>]
    let inline contains (key : int<'Tag>) (set : TagSet<'Tag>) : bool =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.Contains (int key)

    //
    [<CompiledName("Union")>]
    let inline union (set1 : TagSet<'Tag>) (set2 : TagSet<'Tag>) : TagSet<'Tag> =
        // Retype as IntSet
        let set1 : IntSet = retype set1
        let set2 : IntSet = retype set2

        // Preconditions
        checkNonNull "set1" set1
        checkNonNull "set2" set2

        set1.Union set2
        |> retype

    //
    [<CompiledName("OfSeq")>]
    let inline ofSeq source : TagSet<'Tag> =
        // Preconditions are checked by the member.
        IntSet.OfSeq source
        |> retype

    //
    [<CompiledName("OfList")>]
    let inline ofList source : TagSet<'Tag> =
        // Preconditions are checked by the member.
        IntSet.OfList source
        |> retype

    //
    [<CompiledName("OfArray")>]
    let inline ofArray source : TagSet<'Tag> =
        // Preconditions are checked by the member.
        IntSet.OfArray source
        |> retype

    //
    [<CompiledName("OfSet")>]
    let inline ofSet source : TagSet<'Tag> =
        // Preconditions are checked by the member.
        IntSet.OfSet source
        |> retype

    //
    [<CompiledName("ToSeq")>]
    let inline toSeq (set : TagSet<'Tag>) : seq<int<'Tag>> =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.ToSeq ()
        |> retype

    //
    [<CompiledName("ToList")>]
    let inline toList (set : TagSet<'Tag>) =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.ToList ()

    //
    [<CompiledName("ToArray")>]
    let inline toArray (set : TagSet<'Tag>) =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.ToArray ()

    //
    [<CompiledName("ToSet")>]
    let inline toSet (set : TagSet<'Tag>) =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.ToSet ()

    //
    [<CompiledName("Iter")>]
    let inline iter (action : int<'Tag> -> unit) (set : TagSet<'Tag>) =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.Iterate (retype action)

    //
    [<CompiledName("IterBack")>]
    let inline iterBack (action : int<'Tag> -> unit) (set : TagSet<'Tag>) =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.IterateBack (retype action)

    //
    [<CompiledName("Fold")>]
    let inline fold (folder : 'State -> int<'Tag> -> 'State) (state : 'State) (set : TagSet<'Tag>) =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.Fold (retype folder, state)

    //
    [<CompiledName("FoldBack")>]
    let inline foldBack (folder : int<'Tag> -> 'State -> 'State) (set : TagSet<'Tag>) (state : 'State) =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.FoldBack (retype folder, state)

    //
    [<CompiledName("Choose")>]
    let inline choose (chooser : int<'Tag1> -> int<'Tag2> option) (set : TagSet<'Tag1>) : TagSet<'Tag2> =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.Choose (retype chooser)
        |> retype

    //
    [<CompiledName("Filter")>]
    let inline filter (predicate : int<'Tag> -> bool) (set : TagSet<'Tag>) : TagSet<'Tag> =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.Filter (retype predicate)
        |> retype

    //
    [<CompiledName("Map")>]
    let inline map (mapping : int<'Tag1> -> int<'Tag2>) (set : TagSet<'Tag1>) : TagSet<'Tag2> =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.Map (retype mapping)
        |> retype

    //
    [<CompiledName("Partition")>]
    let inline partition (predicate : int<'Tag> -> bool) (set : TagSet<'Tag>) : TagSet<'Tag> * TagSet<'Tag> =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        let set1, set2 = set.Partition (retype predicate)
        (retype set1), (retype set2)

    //
    [<CompiledName("Exists")>]
    let inline exists (predicate : int<'Tag> -> bool) (set : TagSet<'Tag>) : bool =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.Exists (retype predicate)

    //
    [<CompiledName("Forall")>]
    let inline forall (predicate : int<'Tag> -> bool) (set : TagSet<'Tag>) : bool =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.Forall (retype predicate)

    //
    [<CompiledName("TryPick")>]
    let inline tryPick (picker : int<'Tag> -> 'T option) (set : TagSet<'Tag>) : 'T option =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.TryPick (retype picker)

    //
    [<CompiledName("Pick")>]
    let inline pick (picker : int<'Tag> -> 'T option) (set : TagSet<'Tag>) : 'T =
        // Retype as IntSet
        let set : IntSet = retype set

        // Preconditions
        checkNonNull "set" set

        set.Pick (retype picker)

#endif

