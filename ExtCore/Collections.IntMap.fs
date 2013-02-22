﻿(*

Copyright 2010-2012 TidePowerd Ltd.
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


(* TODO :   Implement Okasaki and Gill's data structure from:
            "Fast Mergeable Integer Maps".

            Also implement a "tagged" version which uses ints with measure types.
            (Call this a TagMap.)
            Maybe also implement an EnumMap?
            
            This data structure should implement IDictionary<_,_> like the standard F# Map. *)
(* TODO :   Modify some of the operations below to use CPS to avoid overhead of stack frames. *)

//
module internal BitOps =
    //
    let inline zeroBit (k, m) : bool =
        k &&& m = GenericZero

    //
    let inline mask (k, m) =
        (k ||| (m - GenericOne)) &&& ~~~m

    //
    let inline matchPrefix (k, p, m) =
        mask (k, m) = p

    //
    let inline lowestBit (x : int) : uint32 =
        uint32 (x &&& -x)

    // Returns the greatest power-of-two value contained within 'x'.
    let inline private highestBit (x, m) =
        // Zero all bits below m
        let x' = x &&& ~~~(m - 1u)

        let rec highb x =
            let m = lowestBit (int x)
            if x = m then m
            else highb (x - m)

        highb x'

    /// Finds the first bit at which p0 and p1 disagree.
    /// Returns a power-of-two value containing this (and only this) bit.
    let inline branchingBit (p0, p1) : uint32 =
        highestBit (p0 ^^^ p1, 1u)


    (* TODO :   Re-implement the 'highestBit' function using the Count Leading Zeros (clz) operation.
                This should be significantly faster than our current implementation (or even Okasaki's
                original implementation, since 'clz' can be implemented by just a few operations
                (or a CPU intrinsic) using the algorithm based on de Bruijn sequences.

                It may be possible to further optimize this code by replacing the table with
                a 'match' (which would be compiled into a 'switch' IL OpCode). This change
                won't be made until we can profile and confirm it's actually faster. *)
    (*

    //
    let internal deBruijnBitPosition = [|
        0u; 9u; 1u; 10u; 13u; 21u; 2u; 29u; 11u; 14u; 16u; 18u; 22u; 25u; 3u; 30u;
        8u; 12u; 20u; 28u; 15u; 17u; 24u; 7u; 19u; 27u; 23u; 6u; 26u; 5u; 4u; 31u; |]

    /// Rounds a 32-bit integer down to one less
    /// than the next-lowest power-of-two.
    let inline private roundDown32 (v : uint32) =
        assert (v >= GenericZero)
        let v = v ||| (v >>> 1)
        let v = v ||| (v >>> 2)
        let v = v ||| (v >>> 4)
        let v = v ||| (v >>> 8)
        let v = v ||| (v >>> 16)
        v

    /// Computes the index of the de Bruijn sequence.
    let inline private deBruijnArrayIndex (v : uint32) : uint32 =
        ((roundDown32 v) * 0x07C4ACDDu) >>> 27

    /// Computes the base-2 logarithm (log_2) of a 32-bit integer.
    let inline private int32Log2 (v : uint32) : uint32 =
        deBruijnBitPosition.[int <| deBruijnArrayIndex v]

    /// Count Leading Zeros (clz).
    let inline private countLeadingZeros (x : uint32) : uint32 =
        if x = 0u then 32u
        else
            32u - (int32Log2 x)

    /// Finds the first bit at which p0 and p1 disagree.
    let branchingBit' (p0, p1) : uint32 =
        match p0 ^^^ p1 with
        | 0u -> 0u
        | x ->
            1u <<< (int <| int32Log2 x)
    *)


open BitOps

/// A Patricia trie implementation.
/// Used as the underlying data structure for IntMap (and TagMap).
type internal PatriciaMap<'T> =
    | Empty
    // Key * Value
    | Lf of uint32 * 'T
    // Prefix * Mask * Left-Child * Right-Child
    | Br of uint32 * uint32 * PatriciaMap<'T> * PatriciaMap<'T>

    //
    static member TryFind (key, map : PatriciaMap<'T>) =
        match map with
        | Empty ->
            None
        | Lf (j, x) ->
            if j = key then Some x
            else None
        | Br (p, m, t0, t1) ->
            PatriciaMap.TryFind (
                key, (if key <= p then t0 else t1))

    //
    static member ContainsKey (key, map : PatriciaMap<'T>) =
        match map with
        | Empty ->
            false
        | Lf (j, _) ->
            key = j
        | Br (_, m, t0, t1) ->
            PatriciaMap.ContainsKey
                (key, (if zeroBit (key, m) then t0 else t1))

    //
    static member Count (map : PatriciaMap<'T>) : int =
        match map with
        | Empty -> 0
        | Lf (_,_) -> 1
        | Br (_,_,_,_) as t ->
            // TODO : Run some experiments to determine if this is a good initial capacity,
            // or if there is a more suitable value.
            let stack = System.Collections.Generic.Stack (64)

            // Add the initial tree to the stack.
            stack.Push t

            // Traverse the tree, counting the elements by using the mutable stack.
            let mutable count = 0u

            while stack.Count <> 0 do
                match stack.Pop () with
                | Empty -> ()
                | Lf (_,_) ->
                    count <- count + 1u
                    
                (* OPTIMIZATION :   When one or both children of this node are leaves,
                                    we handle them directly since it's a little faster. *)
                | Br (_, _, Lf (_,_), Lf (_,_)) ->
                    count <- count + 2u
                    
                | Br (_, _, Lf (_,_), child)
                | Br (_, _, child, Lf (_,_)) ->
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
    static member Remove (key, map : PatriciaMap<'T>) =
        match map with
        | Empty ->
            Empty
        | Lf (j, _) as t ->
            if j = key then Empty
            else t
        
        | Br (p, m, t0, t1) as t ->
            if matchPrefix (key, p, m) then
                if zeroBit (key, m) then
                    match PatriciaMap.Remove (key, t0) with
                    | Empty -> t1
                    | t0 ->
                        Br (p, m, t0, t1)
                else
                    match PatriciaMap.Remove (key, t1) with
                    | Empty -> t0
                    | t1 ->
                        Br (p, m, t0, t1)
            else t

    //
    static member (*inline*) private Join (p0, t0 : PatriciaMap<'T>, p1, t1) =
        let m = branchingBit' (p0, p1)
        let p = mask (p0, m)
        if zeroBit (p0, m) then
            Br (p, m, t0, t1)
        else
            Br (p, m, t1, t0)

    //
    static member Insert (key, value : 'T, map) =
        // TODO : Lift this recursive function out into a private member
        let rec ins = function
            | Empty ->
                Lf (key, value)
            | Lf (j, y) as t ->
                let newLeaf = Lf (key, value)
                if j = key then
                    newLeaf
                else
                    PatriciaMap.Join (key, newLeaf, j, t)

            | Br (p, m, t0, t1) as t ->
                if matchPrefix (key, p, m) then
                    if zeroBit (key, m) then
                        let left = ins t0
                        Br (p, m, left, t1)
                    else
                        let right = ins t1
                        Br (p, m, t0, right)
                else
                    PatriciaMap.Join (key, Lf (key, value), p, t)

        // Call the implementation function.
        ins map

    //
    static member Merge (s, t) : PatriciaMap<'T> =
        match s, t with
        | Empty, t
        | t, Empty ->
            t
        | Lf (k, x), t ->
            PatriciaMap.Insert (k, x, t)
        | (Br (p, m, s0, s1) as s), Lf (k, x) ->
            if matchPrefix (k, p, m) then
                if zeroBit (k, m) then
                    let left = PatriciaMap.Insert (k, x, s0)
                    Br (p, m, left, s1)
                else
                    let right = PatriciaMap.Insert (k, x, s1)
                    Br (p, m, s0, right)
            else
                PatriciaMap.Join (k, Lf (k, x), p, s)

        | (Br (p, m, s0, s1) as s), (Br (q, n, t0, t1) as r) ->
            if m = n && p = q then
                // The trees have the same prefix. Merge the subtrees.
                let left = PatriciaMap.Merge (s0, t0)
                let right = PatriciaMap.Merge (s1, t1)
                Br (p, m, left, right)
                
            elif m < n && matchPrefix (q, p, m) then
                // q contains p. Merge t with a subtree of s.
                if zeroBit (q, m) then
                    let left = PatriciaMap.Merge (s0, t)
                    Br (p, m, left, s1)
                else
                    let right = PatriciaMap.Merge (s1, t)
                    Br (p, m, s0, right)

            elif m > n && matchPrefix (p, q, n) then
                // p contains q. Merge s with a subtree of t.
                if zeroBit (p, n) then
                    let left = PatriciaMap.Merge (s, t0)
                    Br (q, n, left, t1)
                else
                    let right = PatriciaMap.Merge (s, t1)
                    Br (q, n, t0, right)
            else
                // The prefixes disagree.
                PatriciaMap.Join (p, s, q, t)

    //
    static member OfSeq (source : seq<int * 'T>) : PatriciaMap<'T> =
        (Empty, source)
        ||> Seq.fold (fun trie (key, value) ->
            PatriciaMap.Insert (uint32 key, value, trie))

    //
    static member OfList (source : (int * 'T) list) : PatriciaMap<'T> =
        // Preconditions
        checkNonNull "source" source

        (Empty, source)
        ||> List.fold (fun trie (key, value) ->
            PatriciaMap.Insert (uint32 key, value, trie))

    //
    static member OfArray (source : (int * 'T)[]) : PatriciaMap<'T> =
        // Preconditions
        checkNonNull "source" source

        (Empty, source)
        ||> Array.fold (fun trie (key, value) ->
            PatriciaMap.Insert (uint32 key, value, trie))

    //
    static member OfMap (source : Map<int, 'T>) : PatriciaMap<'T> =
        // Preconditions
        checkNonNull "source" source

        (Empty, source)
        ||> Map.fold (fun trie key value ->
            PatriciaMap.Insert (uint32 key, value, trie))

    //
    member this.Iterate (action : int -> 'T -> unit) : unit =
        match this with
        | Empty -> ()
        | Lf (k, x) ->
            action (int k) x
        | Br (_,_,_,_) as t ->
            let action = FSharpFunc<_,_,_>.Adapt action

            // TODO : Run some experiments to determine if this is a good initial capacity,
            // or if there is a more suitable value.
            let stack = System.Collections.Generic.Stack (64)

            // Add the initial tree to the stack.
            stack.Push t

            // Loop until we've processed the entire tree.
            while stack.Count <> 0 do
                match stack.Pop () with
                | Empty -> ()
                | Lf (k, x) ->
                    action.Invoke (int k, x)
                    
                (* OPTIMIZATION :   When one or both children of this node are leaves,
                                    we handle them directly since it's a little faster. *)
                | Br (_, _, Lf (k, x), Lf (j, y)) ->
                    action.Invoke (int k, x)
                    action.Invoke (int j, y)
                    
                | Br (_, _, Lf (k, x), right) ->
                    // Only handle the case where the left child is a leaf
                    // -- otherwise the traversal order would be altered.
                    action.Invoke (int k, x)
                    stack.Push right

                | Br (_, _, left, right) ->
                    // Push both children onto the stack and recurse to process them.
                    // NOTE : They're pushed in the opposite order we want to visit them!
                    stack.Push right
                    stack.Push left

    //
    member this.IterateBack (action : int -> 'T -> unit) : unit =
        match this with
        | Empty -> ()
        | Lf (k, x) ->
            action (int k) x
        | Br (_,_,_,_) as t ->
            let action = FSharpFunc<_,_,_>.Adapt action

            // TODO : Run some experiments to determine if this is a good initial capacity,
            // or if there is a more suitable value.
            let stack = System.Collections.Generic.Stack (64)

            // Add the initial tree to the stack.
            stack.Push t

            // Loop until we've processed the entire tree.
            while stack.Count <> 0 do
                match stack.Pop () with
                | Empty -> ()
                | Lf (k, x) ->
                    action.Invoke (int k, x)
                    
                (* OPTIMIZATION :   When one or both children of this node are leaves,
                                    we handle them directly since it's a little faster. *)
                | Br (_, _, Lf (k, x), Lf (j, y)) ->
                    action.Invoke (int j, y)
                    action.Invoke (int k, x)
                    
                | Br (_, _, left, Lf (k, x)) ->
                    // Only handle the case where the right child is a leaf
                    // -- otherwise the traversal order would be altered.
                    action.Invoke (int k, x)
                    stack.Push left

                | Br (_, _, left, right) ->
                    // Push both children onto the stack and recurse to process them.
                    // NOTE : They're pushed in the opposite order we want to visit them!
                    stack.Push left
                    stack.Push right

    //
    member this.Fold (folder : 'State -> int -> 'T -> 'State, state : 'State) : 'State =
        match this with
        | Empty ->
            state
        | Lf (k, x) ->
            folder state (int k) x
        | Br (_,_,_,_) as t ->
            let folder = FSharpFunc<_,_,_,_>.Adapt folder

            // TODO : Run some experiments to determine if this is a good initial capacity,
            // or if there is a more suitable value.
            let stack = System.Collections.Generic.Stack (64)

            // Add the initial tree to the stack.
            stack.Push t

            /// Loop until we've processed the entire tree.
            let mutable state = state
            while stack.Count <> 0 do
                match stack.Pop () with
                | Empty -> ()
                | Lf (k, x) ->
                    state <- folder.Invoke (state, int k, x)
                    
                (* OPTIMIZATION :   When one or both children of this node are leaves,
                                    we handle them directly since it's a little faster. *)
                | Br (_, _, Lf (k, x), Lf (j, y)) ->
                    state <- folder.Invoke (state, int k, x)
                    state <- folder.Invoke (state, int j, y)
                    
                | Br (_, _, Lf (k, x), right) ->
                    // Only handle the case where the left child is a leaf
                    // -- otherwise the traversal order would be altered.
                    state <- folder.Invoke (state, int k, x)
                    stack.Push right

                | Br (_, _, left, right) ->
                    // Push both children onto the stack and recurse to process them.
                    // NOTE : They're pushed in the opposite order we want to visit them!
                    stack.Push right
                    stack.Push left

            // Return the final state value.
            state

    //
    member this.FoldBack (folder : int -> 'T -> 'State -> 'State, state : 'State) : 'State =
        match this with
        | Empty ->
            state
        | Lf (k, x) ->
            folder (int k) x state
        | Br (_,_,_,_) as t ->
            let folder = FSharpFunc<_,_,_,_>.Adapt folder

            // TODO : Run some experiments to determine if this is a good initial capacity,
            // or if there is a more suitable value.
            let stack = System.Collections.Generic.Stack (64)

            // Add the initial tree to the stack.
            stack.Push t

            /// Loop until we've processed the entire tree.
            let mutable state = state
            while stack.Count <> 0 do
                match stack.Pop () with
                | Empty -> ()
                | Lf (k, x) ->
                    state <- folder.Invoke (int k, x, state)
                    
                (* OPTIMIZATION :   When one or both children of this node are leaves,
                                    we handle them directly since it's a little faster. *)
                | Br (_, _, Lf (k, x), Lf (j, y)) ->
                    state <- folder.Invoke (int j, y, state)
                    state <- folder.Invoke (int k, x, state)
                    
                | Br (_, _, left, Lf (k, x)) ->
                    // Only handle the case where the right child is a leaf
                    // -- otherwise the traversal order would be altered.
                    state <- folder.Invoke (int k, x, state)
                    stack.Push left

                | Br (_, _, left, right) ->
                    // Push both children onto the stack and recurse to process them.
                    // NOTE : They're pushed in the opposite order we want to visit them!
                    stack.Push left
                    stack.Push right

            // Return the final state value.
            state


//
[<Sealed>]
[<DebuggerTypeProxy(typedefof<IntMapDebuggerProxy<int>>)>]
[<DebuggerDisplay("Count = {Count}")>]
type IntMap<'T> private (trie : PatriciaMap<'T>) =
    /// The empty IntMap.
    static member Empty
        with get () : IntMap<'T> =
            IntMap Empty

    /// The number of bindings in the IntMap.
    member __.Count
        with get () : int =
            PatriciaMap.Count trie

    /// Is the map empty?
    member __.IsEmpty
        with get () =
            match trie with
            | Empty -> true
            | _ -> false

    /// Look up an element in the IntMap returning a Some value if the
    /// element is in the domain of the IntMap and None if not.
    member __.TryFind (key : int) : 'T option =
        PatriciaMap.TryFind (uint32 key, trie)

    /// Look up an element in the IntMap, raising KeyNotFoundException
    /// if no binding exists in the IntMap.
    member __.Find (key : int) : 'T =
        match PatriciaMap.TryFind (uint32 key, trie) with
        | Some x -> x
        | None ->
            // TODO : Add a better error message which includes the key.
            raise <| System.Collections.Generic.KeyNotFoundException ()

    /// Returns a new IntMap with the binding added to this IntMap.
    member __.Add (key : int, value : 'T) : IntMap<'T> =
        IntMap (
            PatriciaMap.Insert (uint32 key, value, trie))

    /// Removes an element from the domain of the IntMap.
    /// No exception is raised if the element is not present.
    member __.Remove (key : int) : IntMap<'T> =
        IntMap (
            PatriciaMap.Remove (uint32 key, trie))

    /// Tests if an element is in the domain of the IntMap.
    member __.ContainsKey (key : int) : bool =
        PatriciaMap.ContainsKey (uint32 key, trie)

    /// The IntMap containing the given binding.
    static member Singleton (key : int, value : 'T) : IntMap<'T> =
        IntMap (
            Lf (uint32 key, value))

    /// Returns a new IntMap made from the given bindings.
    static member OfSeq (source : seq<int * 'T>) : IntMap<'T> =
        // Preconditions
        checkNonNull "source" source

        IntMap (PatriciaMap.OfSeq source)

    /// Returns a new IntMap made from the given bindings.
    static member OfList (source : (int * 'T) list) : IntMap<'T> =
        // Preconditions
        checkNonNull "source" source

        // OPTIMIZATION : If the source is empty return immediately.
        if List.isEmpty source then
            IntMap.Empty
        else
            IntMap (PatriciaMap.OfList source)

    /// Returns a new IntMap made from the given bindings.
    static member OfArray (source : (int * 'T)[]) : IntMap<'T> =
        // Preconditions
        checkNonNull "source" source

        // OPTIMIZATION : If the source is empty return immediately.
        if Array.isEmpty source then
            IntMap.Empty
        else
            IntMap (PatriciaMap.OfArray source)

    /// Returns a new IntMap made from the given bindings.
    static member OfMap (source : Map<int, 'T>) : IntMap<'T> =
        // Preconditions
        checkNonNull "source" source

        // OPTIMIZATION : If the source is empty return immediately.
        if Map.isEmpty source then
            IntMap.Empty
        else
            IntMap (PatriciaMap.OfMap source)

    //
    member __.ToArray () : (int * 'T)[] =
        let elements = ResizeArray ()
        trie.Iterate (FuncConvert.FuncFromTupled<_,_,_> elements.Add)
        elements.ToArray ()

    //
    member __.ToList () : (int * 'T) list =
        trie.FoldBack ((fun k v list -> (k, v) :: list), [])

    //
    member __.ToMap () : Map<int, 'T> =
        trie.FoldBack (Map.add, Map.empty)

    //
    member internal __.ToKvpArray () : KeyValuePair<int, 'T>[] =
        let elements = ResizeArray (1024)

        trie.Iterate <| fun key value ->
            elements.Add (
                KeyValuePair (key, value))

        elements.ToArray ()

    //
    member __.Iterate (action : int -> 'T -> unit) : unit =
        trie.Iterate action

    //
    member __.IterateBack (action : int -> 'T -> unit) : unit =
        trie.IterateBack action

    //
    member __.Fold (folder : 'State -> int -> 'T -> 'State, state : 'State) : 'State =
        trie.Fold (folder, state)

    //
    member __.FoldBack (folder : int -> 'T -> 'State -> 'State, state : 'State) : 'State =
        trie.FoldBack (folder, state)


//
and [<Sealed>]
    internal IntMapDebuggerProxy<'T> (map : IntMap<'T>) =

    [<DebuggerBrowsable(DebuggerBrowsableState.RootHidden)>]
    member __.Items
        with get () : KeyValuePair<int, 'T>[] =
            map.ToKvpArray ()


//
[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module IntMap =
    /// The empty IntMap.
    [<GeneralizableValue>]
    [<CompiledName("Empty")>]
    let empty<'T> =
        IntMap<'T>.Empty

    /// Is the map empty?
    let inline isEmpty (map : IntMap<'T>) : bool =
        // Preconditions
        checkNonNull "map" map

        map.IsEmpty

    /// Returns the number of bindings in the IntMap.
    let inline count (map : IntMap<'T>) : int =
        // Preconditions
        checkNonNull "map" map
        
        map.Count

    /// The IntMap containing the given binding.
    let inline singleton (key : int) (value : 'T) : IntMap<'T> =
        IntMap.Singleton (key, value)

    /// Look up an element in the IntMap returning a Some value if the
    /// element is in the domain of the IntMap and None if not.
    let inline tryFind (key : int) (map : IntMap<'T>) : 'T option =
        // Preconditions
        checkNonNull "map" map
        
        map.TryFind key
        
    //
    let inline find (key : int) (map : IntMap<'T>) : 'T =
        // Preconditions
        checkNonNull "map" map

        map.Find key

    //
    let inline add (key : int) (value : 'T) (map : IntMap<'T>) : IntMap<'T> =
        // Preconditions
        checkNonNull "map" map

        map.Add (key, value)

    //
    let inline remove (key : int) (map : IntMap<'T>) : IntMap<'T> =
        // Preconditions
        checkNonNull "map" map

        map.Remove key

    //
    let inline containsKey (key : int) (map : IntMap<'T>) : bool =
        // Preconditions
        checkNonNull "map" map

        map.ContainsKey key

    //
    let inline ofSeq source : IntMap<'T> =
        // Preconditions are checked by the member.
        IntMap.OfSeq source

    //
    let inline ofList source : IntMap<'T> =
        // Preconditions are checked by the member.
        IntMap.OfList source

    //
    let inline ofArray source : IntMap<'T> =
        // Preconditions are checked by the member.
        IntMap.OfArray source

    //
    let inline ofMap source : IntMap<'T> =
        // Preconditions are checked by the member.
        IntMap.OfMap source

    //
    let inline toArray (map : IntMap<'T>) =
        // Preconditions
        checkNonNull "map" map

        map.ToArray ()

    //
    let inline toList (map : IntMap<'T>) =
        // Preconditions
        checkNonNull "map" map

        map.ToList ()

    //
    let inline toMap (map : IntMap<'T>) =
        // Preconditions
        checkNonNull "map" map

        map.ToMap ()

    //
    let inline iter (action : int -> 'T -> unit) (map : IntMap<'T>) =
        // Preconditions
        checkNonNull "map" map

        map.Iterate action

    //
    let inline iterBack (action : int -> 'T -> unit) (map : IntMap<'T>) =
        // Preconditions
        checkNonNull "map" map

        map.IterateBack action

    //
    let inline fold (folder : 'State -> int -> 'T -> 'State) (state : 'State) (map : IntMap<'T>) =
        // Preconditions
        checkNonNull "map" map

        map.Fold (folder, state)

    //
    let inline foldBack (folder : int -> 'T -> 'State -> 'State) (map : IntMap<'T>) (state : 'State) =
        // Preconditions
        checkNonNull "map" map

        map.FoldBack (folder, state)


    (* TEMP :   The implementations of the functions below should be moved into IntMap members,
                and replaced with optimized implementations where possible. *)

    //
    let choose (chooser : int -> 'T -> 'U option) (map : IntMap<'T>) : IntMap<'U> =
        // Preconditions
        checkNonNull "map" map

        let chooser = FSharpFunc<_,_,_>.Adapt chooser

        (IntMap.Empty, map)
        ||> fold (fun chosenMap key value ->
            match chooser.Invoke (key, value) with
            | None ->
                chosenMap
            | Some newValue ->
                chosenMap.Add (key, newValue))

    //
    let filter (predicate : int -> 'T -> bool) (map : IntMap<'T>) : IntMap<'T> =
        // Preconditions
        checkNonNull "map" map

        let predicate = FSharpFunc<_,_,_>.Adapt predicate

        (map, map)
        ||> fold (fun filteredMap key value ->
            if predicate.Invoke (key, value) then
                filteredMap
            else
                filteredMap.Remove key)

    //
    let map (mapping : int -> 'T -> 'U) (map : IntMap<'T>) : IntMap<'U> =
        // Preconditions
        checkNonNull "map" map

        let mapping = FSharpFunc<_,_,_>.Adapt mapping

        (IntMap.Empty, map)
        ||> fold (fun map key value ->
            map.Add (key, mapping.Invoke (key, value)))

    //
    let partition (predicate : int -> 'T -> bool) (map : IntMap<'T>) : IntMap<'T> * IntMap<'T> =
        // Preconditions
        checkNonNull "map" map

        let predicate = FSharpFunc<_,_,_>.Adapt predicate

        ((IntMap.Empty, IntMap.Empty), map)
        ||> fold (fun (trueMap, falseMap) key value ->
            if predicate.Invoke (key, value) then
                trueMap.Add (key, value),
                falseMap
            else
                trueMap,
                falseMap.Add (key, value))

    //
    let mapPartition (partitioner : int -> 'T -> Choice<'U, 'V>) (map : IntMap<'T>) : IntMap<'U> * IntMap<'V> =
        // Preconditions
        checkNonNull "map" map

        let partitioner = FSharpFunc<_,_,_>.Adapt partitioner

        ((IntMap.Empty, IntMap.Empty), map)
        ||> fold (fun (map1, map2) key value ->
            match partitioner.Invoke (key, value) with
            | Choice1Of2 value ->
                map1.Add (key, value),
                map2
            | Choice2Of2 value ->
                map1,
                map2.Add (key, value))



    // TODO
    // exists
    // findKey
    // forall
    // pick
    // toSeq
    // tryFindKey
    // tryPick
    // union


(* TODO : Can we just implement TagMap as a measure-annotated IntMap? *)
(*
[<MeasureAnnotatedAbbreviation>]
type TagMap<[<Measure>] 'Tag, 'T> =
    IntMap<'T>
*)

