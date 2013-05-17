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

namespace ExtCore.Collections

open System.Collections.Generic
open System.Diagnostics
open LanguagePrimitives
open OptimizedClosures
open ExtCore
open PatriciaTrieConstants
open BitOps


/// An association list implementing a simple map-like data structure.
/// This is used within PatriciaHashMap to handle hash collisions in a manner reminiscent of a hashtable.
type private ListMap<'Key, [<EqualityConditionalOn; ComparisonConditionalOn>] 'T when 'Key : equality> =
    /// The tail (last element) in the list.
    | Tail of 'Key * 'T
    /// A list element (key-value pair) along with the rest of the list.
    | Element of ListMap<'Key, 'T> * 'Key * 'T

    /// Returns the number of bindings in the map.
    static member private CountImpl (listMap : ListMap<'Key, 'T>, acc) : int =
        match listMap with
        | Tail (_,_) ->
            acc + 1
        | Element (listMap,_,_) ->
            ListMap.CountImpl (listMap, acc + 1)

    /// Returns the number of bindings in the map.
    static member Count (listMap : ListMap<'Key, 'T>) : int =
        // Call the recursive implementation.
        ListMap.CountImpl (listMap, 0)

    //
    static member TryFind (key : 'Key, listMap : ListMap<'Key, 'T>) : 'T option =
        match listMap with
        | Tail (k, v) ->
            if k = key then Some v
            else None
        | Element (listMap, k, v) ->
            if k = key then Some v
            else ListMap.TryFind (key, listMap)

    //
    static member ContainsKey (key : 'Key, listMap : ListMap<'Key, 'T>) : bool =
        match listMap with
        | Tail (k, _) ->
            k = key
        | Element (listMap, k, _) ->
            k = key || ListMap.ContainsKey (key, listMap)

    //
    static member private Rewind (stack : KeyValuePair<'Key, 'T> list, listMap : ListMap<'Key, 'T>) : ListMap<'Key, 'T> =
        match stack with
        | [] ->
            listMap
        | kvp :: stack ->
            ListMap.Rewind (
                stack,
                Element (listMap, kvp.Key, kvp.Value))

    //
    static member RemoveImpl (stack : KeyValuePair<'Key, 'T> list, listMap : ListMap<'Key, 'T>, original, key : 'Key) : ListMap<'Key, 'T> option =
        match listMap with
        | Tail (k, _) ->
            if k = key then
                match stack with
                | [] -> None
                | kvp :: stack ->
                    ListMap.Rewind (stack, Tail (kvp.Key, kvp.Value))
                    |> Some
            else
                // The specified key isn't in the map, so return the original map without changing it.
                Some original

        | Element (listMap, k, v) ->
            if k = key then
                Some <| ListMap.Rewind (stack, listMap)
            else
                ListMap.RemoveImpl (KeyValuePair (k, v) :: stack, listMap, original, key)

    //
    static member Remove (key : 'Key, listMap : ListMap<'Key, 'T>) : ListMap<'Key, 'T> option =
        match listMap with
        | Tail (k, _) ->
            if k = key then None
            else Some listMap

        | Element (listMap, k, v) ->
            if k = key then Some listMap
            else
                ListMap.RemoveImpl ([KeyValuePair (k, v)], listMap, listMap, key)

    //
    static member private AddImpl (stack : KeyValuePair<'Key, 'T> list, listMap : ListMap<'Key, 'T>, original, key, value) : ListMap<'Key, 'T> =
        match listMap with
        | Tail (k, v) ->
            let c = Unchecked.compare k key
            if c = 0 then 
                // If the new value is the same as the existing value,
                // return the original map since it doesn't need to be modified.
                if Unchecked.equals v value then original
                else
                    ListMap.Rewind (stack, Tail (k, value))
            elif c < 0 then
                // Add the new binding to the end of the listMap.
                let listMap = Element (Tail (key, value), k, v)
                ListMap.Rewind (stack, listMap)

            else
                // Insert the new binding before the tail.
                let listMap = Element (listMap, k, v)
                ListMap.Rewind (stack, listMap)

        | Element (listMap, k, v) ->
            let c = Unchecked.compare k key
            if c = 0 then
                // If the new value is the same as the existing value,
                // return the original map since it doesn't need to be modified.
                if Unchecked.equals v value then original
                else
                    ListMap.Rewind (stack, Element (listMap, k, value))
            elif c < 0 then
                // The new binding is inserted at some "later" point in the list.
                ListMap.AddImpl (KeyValuePair (k, v) :: stack, listMap, original, key, value)

            else
                // Add the new binding prior to this binding, then rewind the stack onto
                // the result to produce the new map.
                let listMap = Element (listMap, key, value)
                ListMap.Rewind (stack, listMap)

    //
    static member Add (key : 'Key, value : 'T, listMap : ListMap<'Key, 'T>) : ListMap<'Key, 'T> =
        ListMap.AddImpl ([], listMap, listMap, key, value)

    //
    static member TryAdd (key : 'Key, value : 'T, listMap : ListMap<'Key, 'T>) : ListMap<'Key, 'T> =
        notImpl "ListMap.TryAdd"

    //
    static member ToSeq (listMap : ListMap<'Key, 'T>) =
        seq {
        match listMap with
        | Tail (k, v) ->
            yield (k, v)
        | Element (listMap, k, v) ->
            yield (k, v)
            yield! ListMap.ToSeq listMap
        }

    //
    static member private Reverse (reversed : ListMap<'Key, 'T>, listMap : ListMap<'Key, 'T>) =
        match listMap with
        | Tail (k, v) ->
            Element (reversed, k, v)
        | Element (listMap, k, v) ->
            ListMap.Reverse (Element (reversed, k, v), listMap)

    //
    static member private Reverse (listMap : ListMap<'Key, 'T>) =
        match listMap with
        | Tail (_,_) ->
            listMap
        | Element (listMap, k, v) ->
            ListMap.Reverse (Tail (k, v), listMap)

    //
    static member Iterate (action : FSharpFunc<_,_,_>, listMap : ListMap<'Key, 'T>) =
        match listMap with
        | Tail (k, v) ->
            action.Invoke (k, v)
        | Element (listMap, k, v) ->
            action.Invoke (k, v)
            ListMap.Iterate (action, listMap)

    //
    static member IterateBack (action : FSharpFunc<_,_,_>, listMap : ListMap<'Key, 'T>) =
        ListMap.Iterate (action, ListMap.Reverse listMap)

    //
    static member Fold (folder : FSharpFunc<_,_,_,_>, listMap : ListMap<'Key, 'T>, state : 'State) =
        match listMap with
        | Tail (k, v) ->
            folder.Invoke (state, k, v)
        | Element (listMap, k, v) ->
            let state = folder.Invoke (state, k, v)
            ListMap.Fold (folder, listMap, state)

    //
    static member FoldBack (folder : FSharpFunc<_,_,_,_>, listMap : ListMap<'Key, 'T>, state : 'State) =
        let folder state k v =
            folder.Invoke (k, v, state)
        let folder = FSharpFunc<_,_,_,_>.Adapt folder
        ListMap.Fold (folder, ListMap.Reverse listMap, state)

    //
    static member TryFindKey (predicate : FSharpFunc<_,_,_>, listMap : ListMap<'Key, 'T>) =
        match listMap with
        | Tail (k, v) ->
            if predicate.Invoke (k, v) then
                Some k
            else None
        | Element (listMap, k, v) ->
            if predicate.Invoke (k, v) then
                Some k
            else
                ListMap.TryFindKey (predicate, listMap)

    //
    static member TryPick (picker : FSharpFunc<_,_,_>, listMap : ListMap<'Key, 'T>) : 'U option =
        match listMap with
        | Tail (k, v) ->
            picker.Invoke (k, v)
        | Element (listMap, k, v) ->
            match picker.Invoke (k, v) with
            | None ->
                ListMap.TryPick (picker, listMap)
            | result ->
                result


(* OPTIMIZE :   Some of the functional-style operations on PatriciaHashMap use direct non-tail-recursion;
                performance may be improved if we modify these to use CPS instead. *)

/// A Patricia trie implementation, modified so each of it's 'values'
/// is actually a set implemented with a list.
/// Used as the underlying data structure for HashMap.
[<CompilationRepresentation(CompilationRepresentationFlags.UseNullAsTrueValue)>]
type private PatriciaHashMap<'Key, [<EqualityConditionalOn; ComparisonConditionalOn>] 'T when 'Key : equality> =
    | Empty
    // Key-HashCode * Value
    | Lf of uint32 * ListMap<'Key, 'T>
    // Prefix * Mask * Left-Child * Right-Child
    | Br of uint32 * uint32 * PatriciaHashMap<'Key, 'T> * PatriciaHashMap<'Key, 'T>

    //
    static member TryFind (keyHash, key : 'Key, map : PatriciaHashMap<'Key, 'T>) =
        match map with
        | Empty ->
            None
        | Lf (j, listMap) ->
            if j = keyHash then
                ListMap.TryFind (key, listMap)
            else None
        | Br (_, m, t0, t1) ->
            PatriciaHashMap.TryFind (
                keyHash, key, (if zeroBit (keyHash, m) then t0 else t1))

    //
    static member ContainsKey (keyHash, key : 'Key, map : PatriciaHashMap<'Key, 'T>) =
        match map with
        | Empty ->
            false
        | Lf (j, listMap) ->
            j = keyHash
            && ListMap.ContainsKey (key, listMap)
        | Br (_, m, t0, t1) ->
            PatriciaHashMap.ContainsKey (
                keyHash, key, (if zeroBit (keyHash, m) then t0 else t1))

    //
    static member Count (map : PatriciaHashMap<'Key, 'T>) : int =
        match map with
        | Empty -> 0
        | Lf (_, listMap) ->
            ListMap.Count listMap
        | Br (_,_,_,_) ->
            /// The stack of trie nodes pending traversal.
            let stack = Stack (defaultTraversalStackSize)

            // Add the initial tree to the stack.
            stack.Push map

            // Traverse the tree, counting the elements by using the mutable stack.
            let mutable count = 0u

            while stack.Count <> 0 do
                match stack.Pop () with
                | Empty -> ()
                | Lf (_, listMap) ->
                    count <- count + (uint32 <| ListMap.Count listMap)
                    
                (* OPTIMIZATION :   When one or both children of this node are leaves,
                                    we handle them directly since it's a little faster. *)
                | Br (_, _, Lf (_, listMap1), Lf (_, listMap2)) ->
                    count <-
                        count
                        + (uint32 <| ListMap.Count listMap1)
                        + (uint32 <| ListMap.Count listMap2)
                    
                | Br (_, _, Lf (_, listMap), child)
                | Br (_, _, child, Lf (_, listMap)) ->
                    count <- count + (uint32 <| ListMap.Count listMap)
                    stack.Push child

                | Br (_, _, left, right) ->
                    // Push both children onto the stack and recurse to process them.
                    // NOTE : They're pushed in the opposite order we want to visit them!
                    stack.Push right
                    stack.Push left

            // Return the computed element count.
            int count

    /// Remove the binding with the specified key from the map.
    /// No exception is thrown if the map does not contain a binding for the key.
    static member Remove (keyHash, key : 'Key, map : PatriciaHashMap<'Key, 'T>) =
        match map with
        | Empty ->
            Empty
        | Lf (j, listMap) ->
            if j = keyHash then
                match ListMap.Remove (key, listMap) with
                | None ->
                    Empty
                | Some result ->
                    // OPTIMIZATION : If the result is the same as the input, return the
                    // original map since it wasn't modified.
                    if result === listMap then map
                    else
                        Lf (j, result)
            else map
        
        | Br (p, m, t0, t1) ->
            if matchPrefix (keyHash, p, m) then
                if zeroBit (keyHash, m) then
                    match PatriciaHashMap.Remove (keyHash, key, t0) with
                    | Empty -> t1
                    | left ->
                        // OPTIMIZE : If left === t0 then return the original map instead of creating a new one.
                        Br (p, m, left, t1)
                else
                    match PatriciaHashMap.Remove (keyHash, key, t1) with
                    | Empty -> t0
                    | right ->
                        // OPTIMIZE : If right === t1 then return the original map instead of creating a new one.
                        Br (p, m, t0, right)
            else map

    //
    static member inline private Join (p0, t0 : PatriciaHashMap<'Key, 'T>, p1, t1) =
        let m = branchingBit (p0, p1)
        let p = mask (p0, m)
        if zeroBit (p0, m) then
            Br (p, m, t0, t1)
        else
            Br (p, m, t1, t0)

    /// Insert a binding (key-value pair) into a map, returning a new, updated map.
    static member Add (keyHash, key : 'Key, value : 'T, map) =
        match map with
        | Empty ->
            Lf (keyHash, Tail (key, value))
        | Lf (j, listMap) ->
            if j = keyHash then
                let result = ListMap.Add (key, value, listMap)

                // OPTIMIZATION : If the result is the same as the input, return the original
                // map instead since it wasn't modified.
                if result === listMap then map
                else
                    Lf (j, result)
            else
                PatriciaHashMap.Join (keyHash, Lf (keyHash, Tail (key, value)), j, map)
        | Br (p, m, t0, t1) ->
            if matchPrefix (keyHash, p, m) then
                if zeroBit (keyHash, m) then
                    let left = PatriciaHashMap.Add (keyHash, key, value, t0)

                    // OPTIMIZE : If left === t0 then return the original map instead of creating a new one.
                    Br (p, m, left, t1)
                else
                    let right = PatriciaHashMap.Add (keyHash, key, value, t1)

                    // OPTIMIZE : If right === t1 then return the original map instead of creating a new one.
                    Br (p, m, t0, right)
            else
                PatriciaHashMap.Join (keyHash, Lf (keyHash, Tail (key, value)), p, map)

    /// Insert a binding (key-value pair) into a map, returning a new, updated map.
    /// If a binding already exists for the same key, the map is not altered.
    static member TryAdd (keyHash, key : 'Key, value : 'T, map) =
        match map with
        | Empty ->
            Lf (keyHash, Tail (key, value))
        | Lf (j, listMap) ->
            if j = keyHash then
                let result = ListMap.TryAdd (key, value, listMap)

                // OPTIMIZATION : If the result is the same as the input, return the original
                // map instead since it wasn't modified.
                if result === listMap then map
                else
                    Lf (j, result)
            else
                PatriciaHashMap.Join (keyHash, Lf (keyHash, Tail (key, value)), j, map)
        | Br (p, m, t0, t1) ->
            if matchPrefix (keyHash, p, m) then
                if zeroBit (keyHash, m) then
                    let left = PatriciaHashMap.TryAdd (keyHash, key, value, t0)
                    Br (p, m, left, t1)
                else
                    let right = PatriciaHashMap.TryAdd (keyHash, key, value, t1)
                    Br (p, m, t0, right)
            else
                PatriciaHashMap.Join (keyHash, Lf (keyHash, Tail (key, value)), p, map)

    /// Insert a binding (key-value pair) into a map, returning a new, updated map.
    /// If a binding already exists for the same key, the map is not altered.
    static member TryAdd (key : 'Key, value : 'T, map) =
        let keyHash = uint32 <| hash key
        PatriciaHashMap.TryAdd (keyHash, key, value, map)
(*
    /// Computes the union of two PatriciaHashMaps.
    static member Union (s, t) : PatriciaHashMap<'Key, 'T> =
        match s, t with
        | Br (p, m, s0, s1), Br (q, n, t0, t1) ->
            if m = n then
                if p = q then
                    // The trees have the same prefix. Merge the subtrees.
                    let left = PatriciaHashMap.Union (s0, t0)
                    let right = PatriciaHashMap.Union (s1, t1)
                    Br (p, m, left, right)
                else
                    // The prefixes disagree.
                    PatriciaHashMap.Join (p, s, q, t)

            #if LITTLE_ENDIAN_TRIES
            elif m < n then
            #else
            elif m > n then
            #endif
                if matchPrefix (q, p, m) then
                    // q contains p. Merge t with a subtree of s.
                    if zeroBit (q, m) then
                        let left = PatriciaHashMap.Union (s0, t)
                        Br (p, m, left, s1)
                    else
                        let right = PatriciaHashMap.Union (s1, t)
                        Br (p, m, s0, right)
                else
                    // The prefixes disagree.
                    PatriciaHashMap.Join (p, s, q, t)

            else
                if matchPrefix (p, q, n) then
                    // p contains q. Merge s with a subtree of t.
                    if zeroBit (p, n) then
                        let left = PatriciaHashMap.Union (s, t0)
                        Br (q, n, left, t1)
                    else
                        let right = PatriciaHashMap.Union (s, t1)
                        Br (q, n, t0, right)
                else
                    // The prefixes disagree.
                    PatriciaHashMap.Join (p, s, q, t)

        | Br (p, m, s0, s1), Lf (k, x) ->
            if matchPrefix (k, p, m) then
                if zeroBit (k, m) then
                    let left = PatriciaHashMap.TryAdd (k, x, s0)
                    Br (p, m, left, s1)
                else
                    let right = PatriciaHashMap.TryAdd (k, x, s1)
                    Br (p, m, s0, right)
            else
                PatriciaHashMap.Join (k, Lf (k, x), p, s)

        | Br (_,_,_,_), Empty ->
            s
        | Lf (k, x), _ ->
            PatriciaHashMap.Add (k, x, t)
        | Empty, _ -> t

    /// Compute the intersection of two PatriciaMaps.
    /// If both maps contain a binding with the same key, the binding from
    /// the first map will be used.
    static member Intersect (s, t) : PatriciaHashMap<'Key, 'T> =
        match s, t with
        | Br (p, m, s0, s1), Br (q, n, t0, t1) ->
            if m = n then
                if p <> q then Empty
                else
                    let left = PatriciaHashMap.Intersect (s0, t0)
                    let right = PatriciaHashMap.Intersect (s1, t1)
                    match left, right with
                    | Empty, t
                    | t, Empty -> t
                    | left, right ->
                        Br (p, m, left, right)

            #if LITTLE_ENDIAN_TRIES
            elif m < n then
            #else
            elif m > n then
            #endif
                if matchPrefix (q, p, m) then
                    if zeroBit (q, m) then
                        PatriciaHashMap.Intersect (s0, t)
                    else
                        PatriciaHashMap.Intersect (s1, t)
                else
                    Empty

            else
                if matchPrefix (p, q, n) then
                    if zeroBit (p, n) then
                        PatriciaHashMap.Intersect (s, t0)
                    else
                        PatriciaHashMap.Intersect (s, t1)
                else
                    Empty

        | Br (_, m, s0, s1), Lf (k, y) ->
            let s' = if zeroBit (k, m) then s0 else s1
            match PatriciaHashMap.TryFind (k, s') with
            | Some x ->
                Lf (k, x)
            | None ->
                Empty
            
        | Br (_,_,_,_), Empty ->
            Empty
            
        | Lf (k, x), _ ->
            // Here, we always use the value from the left tree, so as long as the
            // right tree contains a binding with the same key, we just return the left tree.
            if PatriciaHashMap.ContainsKey (k, t) then s
            else Empty

        | Empty, _ ->
            Empty

    /// Compute the difference of two PatriciaMaps.
    static member Difference (s, t) : PatriciaHashMap<'Key, 'T> =
        match s, t with
        | Br (p, m, s0, s1), Br (q, n, t0, t1) ->
            if m = n then
                if p <> q then s
                else
                    let left = PatriciaHashMap.Difference (s0, t0)
                    let right = PatriciaHashMap.Difference (s1, t1)
                    match left, right with
                    | Empty, t
                    | t, Empty -> t
                    | left, right ->
                        Br (p, m, left, right)

            #if LITTLE_ENDIAN_TRIES
            elif m < n then
            #else
            elif m > n then
            #endif
                if matchPrefix (q, p, m) then
                    if zeroBit (q, m) then
                        match PatriciaHashMap.Difference (s0, t) with
                        | Empty -> s1
                        | left ->
                            Br (p, m, left, s1)
                    else
                        match PatriciaHashMap.Difference (s1, t) with
                        | Empty -> s0
                        | right ->
                            Br (p, m, s0, right)
                else s

            else
                if matchPrefix (p, q, n) then
                    if zeroBit (p, n) then
                        PatriciaHashMap.Difference (s, t0)
                    else
                        PatriciaHashMap.Difference (s, t1)
                else s

        | Br (p, m, s0, s1), Lf (k, y) ->
            if matchPrefix (k, p, m) then
                if zeroBit (k, m) then
                    match PatriciaHashMap.Remove (k, s0) with
                    | Empty -> s1
                    | left ->
                        Br (p, m, left, s1)
                else
                    match PatriciaHashMap.Remove (k, s1) with
                    | Empty -> s0
                    | right ->
                        Br (p, m, s0, right)
            else s
            
        | Br (_,_,_,_), Empty ->
            s
        | Lf (k, x), _ ->
            if PatriciaHashMap.ContainsKey (k, t) then Empty
            else s
        | Empty, _ ->
            Empty
*)
(*
    /// <c>IsSubmapOfBy f t1 t2</c> returns <c>true</c> if all keys in t1 are in t2,
    /// and when 'f' returns <c>true</c> when applied to their respective values.
    static member IsSubmapOfBy (predicate : 'T -> 'T -> bool) (t1 : PatriciaHashMap<'Key, 'T>) (t2 : PatriciaHashMap<'Key, 'T>) : bool =
        match t1, t2 with
        | (Br (p1, m1, l1, r1) as t1), (Br (p2, m2, l2, r2) as t2) ->
            if shorter (m1, m2) then
                false
            elif shorter (m2, m1) then
                matchPrefix (p1, p2, m2) && (
                    if zeroBit (p1, m2) then
                        PatriciaHashMap.IsSubmapOfBy predicate t1 l2
                    else
                        PatriciaHashMap.IsSubmapOfBy predicate t1 r2)
            else
                p1 = p2
                && PatriciaHashMap.IsSubmapOfBy predicate l1 l2
                && PatriciaHashMap.IsSubmapOfBy predicate r1 r2
                
        | Br (_,_,_,_), _ ->
            false
        | Lf (k, x), t ->
            match PatriciaHashMap.TryFind (k, t) with
            | None ->
                false
            | Some y ->
                predicate x y
        | Empty, _ ->
            true

    //
    static member private SubmapCmp (predicate : 'T -> 'T -> bool) (t1 : PatriciaHashMap<'Key, 'T>) (t2 : PatriciaHashMap<'Key, 'T>) : int =
        match t1, t2 with
        | (Br (p1, m1, l1, r1) as t1), (Br (p2, m2, l2, r2) as t2) ->
            if shorter (m1, m2) then 1
            elif shorter (m2, m1) then
                if not <| matchPrefix (p1, p2, m2) then 1
                elif zeroBit (p1, m2) then
                    PatriciaHashMap.SubmapCmp predicate t1 l2
                else
                    PatriciaHashMap.SubmapCmp predicate t1 r2
            elif p1 = p2 then
                let left = PatriciaHashMap.SubmapCmp predicate l1 l2
                let right = PatriciaHashMap.SubmapCmp predicate r1 r2
                match left, right with
                | 1, _
                | _, 1 -> 1
                | 0, 0 -> 0
                | _ -> -1
            else
                // The maps are disjoint.
                1

        | Br (_,_,_,_), _ -> 1
        | Lf (kx, x), Lf (ky, y) ->
            if kx = ky && predicate x y then 0
            else 1  // The maps are disjoint.
        | Lf (k, x), t ->
            match PatriciaHashMap.TryFind (k, t) with
            | Some y when predicate x y -> -1
            | _ -> 1    // The maps are disjoint.

        | Empty, Empty -> 0
        | Empty, _ -> -1
*)

    //
    static member OfSeq (source : seq<'Key * 'T>) : PatriciaHashMap<'Key, 'T> =
        (Empty, source)
        ||> Seq.fold (fun trie (key, value) ->
            let keyHash = uint32 <| hash key
            PatriciaHashMap.Add (keyHash, key, value, trie))

    //
    static member OfList (source : ('Key * 'T) list) : PatriciaHashMap<'Key, 'T> =
        // Preconditions
        checkNonNull "source" source

        (Empty, source)
        ||> List.fold (fun trie (key, value) ->
            let keyHash = uint32 <| hash key
            PatriciaHashMap.Add (keyHash, key, value, trie))

    //
    static member OfArray (source : ('Key * 'T)[]) : PatriciaHashMap<'Key, 'T> =
        // Preconditions
        checkNonNull "source" source

        (Empty, source)
        ||> Array.fold (fun trie (key, value) ->
            let keyHash = uint32 <| hash key
            PatriciaHashMap.Add (keyHash, key, value, trie))

//    //
//    static member OfMap (source : Map<'Key, 'T>) : PatriciaHashMap<'Key, 'T> =
//        // Preconditions
//        checkNonNull "source" source
//
//        (Empty, source)
//        ||> Map.fold (fun trie key value ->
//            let keyHash = uint32 <| hash key
//            PatriciaHashMap.Add (keyHash, key, value, trie))

    //
    static member Iterate (action : 'Key -> 'T -> unit, map) : unit =
        match map with
        | Empty -> ()
        | Lf (_, listMap) ->
            let action = FSharpFunc<_,_,_>.Adapt action
            ListMap.Iterate (action, listMap)
        | Br (_,_,_,_) ->
            let action = FSharpFunc<_,_,_>.Adapt action

            /// The stack of trie nodes pending traversal.
            let stack = Stack (defaultTraversalStackSize)

            // Add the initial tree to the stack.
            stack.Push map

            // Loop until we've processed the entire tree.
            while stack.Count <> 0 do
                match stack.Pop () with
                | Empty -> ()
                | Lf (_, listMap) ->
                    ListMap.Iterate (action, listMap)
                    
                (* OPTIMIZATION :   When one or both children of this node are leaves,
                                    we handle them directly since it's a little faster. *)
                | Br (_, _, Lf (_, listMap1), Lf (_, listMap2)) ->
                    ListMap.Iterate (action, listMap1)
                    ListMap.Iterate (action, listMap2)
                    
                | Br (_, _, Lf (_, listMap), right) ->
                    // Only handle the case where the left child is a leaf
                    // -- otherwise the traversal order would be altered.
                    ListMap.Iterate (action, listMap)
                    stack.Push right

                | Br (_, _, left, right) ->
                    // Push both children onto the stack and recurse to process them.
                    // NOTE : They're pushed in the opposite order we want to visit them!
                    stack.Push right
                    stack.Push left

    //
    static member IterateBack (action : 'Key -> 'T -> unit, map) : unit =
        match map with
        | Empty -> ()
        | Lf (_, listMap) ->
            let action = FSharpFunc<_,_,_>.Adapt action
            ListMap.IterateBack (action, listMap)
        | Br (_,_,_,_) ->
            let action = FSharpFunc<_,_,_>.Adapt action

            /// The stack of trie nodes pending traversal.
            let stack = Stack (defaultTraversalStackSize)

            // Add the initial tree to the stack.
            stack.Push map

            // Loop until we've processed the entire tree.
            while stack.Count <> 0 do
                match stack.Pop () with
                | Empty -> ()
                | Lf (_, listMap) ->
                    ListMap.IterateBack (action, listMap)
                    
                (* OPTIMIZATION :   When one or both children of this node are leaves,
                                    we handle them directly since it's a little faster. *)
                | Br (_, _, Lf (_, listMap1), Lf (_, listMap2)) ->
                    ListMap.IterateBack (action, listMap1)
                    ListMap.IterateBack (action, listMap2)
                    
                | Br (_, _, left, Lf (_, listMap)) ->
                    // Only handle the case where the right child is a leaf
                    // -- otherwise the traversal order would be altered.
                    ListMap.IterateBack (action, listMap)
                    stack.Push left

                | Br (_, _, left, right) ->
                    // Push both children onto the stack and recurse to process them.
                    // NOTE : They're pushed in the opposite order we want to visit them!
                    stack.Push left
                    stack.Push right

    //
    static member Fold (folder : 'State -> 'Key -> 'T -> 'State, state : 'State, map) : 'State =
        match map with
        | Empty ->
            state
        | Lf (_, listMap) ->
            let folder = FSharpFunc<_,_,_,_>.Adapt folder
            ListMap.Fold (folder, listMap, state)
        | Br (_,_,_,_) ->
            let folder = FSharpFunc<_,_,_,_>.Adapt folder

            /// The stack of trie nodes pending traversal.
            let stack = Stack (defaultTraversalStackSize)

            // Add the initial tree to the stack.
            stack.Push map

            /// Loop until we've processed the entire tree.
            let mutable state = state
            while stack.Count <> 0 do
                match stack.Pop () with
                | Empty -> ()
                | Lf (_, listMap) ->
                    state <- ListMap.Fold (folder, listMap, state)
                    
                (* OPTIMIZATION :   When one or both children of this node are leaves,
                                    we handle them directly since it's a little faster. *)
                | Br (_, _, Lf (_, listMap1), Lf (_, listMap2)) ->
                    state <- ListMap.Fold (folder, listMap1, state)
                    state <- ListMap.Fold (folder, listMap2, state)
                    
                | Br (_, _, Lf (_, listMap), right) ->
                    // Only handle the case where the left child is a leaf
                    // -- otherwise the traversal order would be altered.
                    state <- ListMap.Fold (folder, listMap, state)
                    stack.Push right

                | Br (_, _, left, right) ->
                    // Push both children onto the stack and recurse to process them.
                    // NOTE : They're pushed in the opposite order we want to visit them!
                    stack.Push right
                    stack.Push left

            // Return the final state value.
            state

    //
    static member FoldBack (folder : 'Key -> 'T -> 'State -> 'State, state : 'State, map) : 'State =
        match map with
        | Empty ->
            state
        | Lf (_, listMap) ->
            let folder = FSharpFunc<_,_,_,_>.Adapt folder
            ListMap.FoldBack (folder, listMap, state)
        | Br (_,_,_,_) ->
            let folder = FSharpFunc<_,_,_,_>.Adapt folder

            /// The stack of trie nodes pending traversal.
            let stack = Stack (defaultTraversalStackSize)

            // Add the initial tree to the stack.
            stack.Push map

            /// Loop until we've processed the entire tree.
            let mutable state = state
            while stack.Count <> 0 do
                match stack.Pop () with
                | Empty -> ()
                | Lf (_, listMap) ->
                    state <- ListMap.FoldBack (folder, listMap, state)
                    
                (* OPTIMIZATION :   When one or both children of this node are leaves,
                                    we handle them directly since it's a little faster. *)
                | Br (_, _, Lf (_, listMap1), Lf (_, listMap2)) ->
                    state <- ListMap.FoldBack (folder, listMap1, state)
                    state <- ListMap.FoldBack (folder, listMap2, state)
                    
                | Br (_, _, left, Lf (_, listMap)) ->
                    // Only handle the case where the right child is a leaf
                    // -- otherwise the traversal order would be altered.
                    state <- ListMap.FoldBack (folder, listMap, state)
                    stack.Push left

                | Br (_, _, left, right) ->
                    // Push both children onto the stack and recurse to process them.
                    // NOTE : They're pushed in the opposite order we want to visit them!
                    stack.Push left
                    stack.Push right

            // Return the final state value.
            state

    //
    static member TryFindKey (predicate : 'Key -> 'T -> bool, map) : 'Key option =
        match map with
        | Empty ->
            None
        | Lf (_, listMap) ->
            let predicate = FSharpFunc<_,_,_>.Adapt predicate
            ListMap.TryFindKey (predicate, listMap)
        | Br (_,_,_,_) ->
            let predicate = FSharpFunc<_,_,_>.Adapt predicate

            /// The stack of trie nodes pending traversal.
            let stack = Stack (defaultTraversalStackSize)

            // Add the initial tree to the stack.
            stack.Push map

            /// Loop until we find a key/value that matches the predicate,
            /// or until we've processed the entire tree.
            let mutable matchingKey = None
            while stack.Count <> 0 && Option.isNone matchingKey do
                match stack.Pop () with
                | Empty -> ()
                | Lf (_, listMap) ->
                    matchingKey <- ListMap.TryFindKey (predicate, listMap)
                    
                (* OPTIMIZATION :   When one or both children of this node are leaves,
                                    we handle them directly since it's a little faster. *)
                | Br (_, _, Lf (_, listMap1), Lf (_, listMap2)) ->
                    matchingKey <- ListMap.TryFindKey (predicate, listMap1)
                    if Option.isNone matchingKey then
                        matchingKey <- ListMap.TryFindKey (predicate, listMap2)
                    
                | Br (_, _, Lf (_, listMap), right) ->
                    // Only handle the case where the left child is a leaf
                    // -- otherwise the traversal order would be altered.
                    matchingKey <- ListMap.TryFindKey (predicate, listMap)
                    stack.Push right

                | Br (_, _, left, right) ->
                    // Push both children onto the stack and recurse to process them.
                    // NOTE : They're pushed in the opposite order we want to visit them!
                    stack.Push right
                    stack.Push left

            // Return the matching key, if one was found.
            matchingKey

    //
    static member TryPick (picker : 'Key -> 'T -> 'U option, map) : 'U option =
        match map with
        | Empty ->
            None
        | Lf (_, listMap) ->
            let picker = FSharpFunc<_,_,_>.Adapt picker
            ListMap.TryPick (picker, listMap)
        | Br (_,_,_,_) ->
            let picker = FSharpFunc<_,_,_>.Adapt picker

            /// The stack of trie nodes pending traversal.
            let stack = Stack (defaultTraversalStackSize)

            // Add the initial tree to the stack.
            stack.Push map

            /// Loop until we find a key/value that matches the picker,
            /// or until we've processed the entire tree.
            let mutable pickedValue = None
            while stack.Count <> 0 && Option.isNone pickedValue do
                match stack.Pop () with
                | Empty -> ()
                | Lf (_, listMap) ->
                    pickedValue <- ListMap.TryPick (picker, listMap)
                    
                (* OPTIMIZATION :   When one or both children of this node are leaves,
                                    we handle them directly since it's a little faster. *)
                | Br (_, _, Lf (_, listMap1), Lf (_, listMap2)) ->
                    pickedValue <-
                        match ListMap.TryPick (picker, listMap1) with
                        | (Some _) as value ->
                            value
                        | None ->
                            ListMap.TryPick (picker, listMap2)
                    
                | Br (_, _, Lf (_, listMap), right) ->
                    // Only handle the case where the left child is a leaf
                    // -- otherwise the traversal order would be altered.
                    pickedValue <- ListMap.TryPick (picker, listMap)
                    stack.Push right

                | Br (_, _, left, right) ->
                    // Push both children onto the stack and recurse to process them.
                    // NOTE : They're pushed in the opposite order we want to visit them!
                    stack.Push right
                    stack.Push left

            // Return the picked value.
            pickedValue

    //
    static member ToSeq (map : PatriciaHashMap<'Key, 'T>) =
        seq {
        match map with
        | Empty -> ()
        | Lf (_, listMap) ->
            yield! ListMap.ToSeq listMap
        
        (* OPTIMIZATION :   When one or both children of this node are leaves,
                            we handle them directly since it's a little faster. *)
        | Br (_, _, Lf (_, listMap1), Lf (_, listMap2)) ->
            yield! ListMap.ToSeq listMap1
            yield! ListMap.ToSeq listMap2

        | Br (_, _, Lf (_, listMap), right) ->
            // Only handle the case where the left child is a leaf
            // -- otherwise the traversal order would be altered.
            yield! ListMap.ToSeq listMap
            yield! PatriciaHashMap.ToSeq right

        | Br (_, _, left, right) ->
            // Recursively visit the children.
            yield! PatriciaHashMap.ToSeq left
            yield! PatriciaHashMap.ToSeq right
        }


/// <summary>Immutable, unordered maps.</summary>
/// <typeparam name="Key">The type of key used by the map.</typeparam>
/// <typeparam name="T">The type of the values stored in the map.</typeparam>
[<Sealed; CompiledName("FSharpHashMap`1")>]
//[<StructuredFormatDisplay("")>]
[<DebuggerDisplay("Count = {Count}")>]
[<DebuggerTypeProxy(typedefof<HashMapDebuggerProxy<int,int>>)>]
type HashMap<'Key, [<EqualityConditionalOn; ComparisonConditionalOn>] 'T when 'Key : equality>
    private (trie : PatriciaHashMap<'Key, 'T>) =
    /// The empty HashMap instance.
    static let empty : HashMap<'Key, 'T> =
        HashMap Empty

    /// The empty HashMap.
    static member Empty
        with get () = empty

    //
    new (elements : seq<'Key * 'T>) =
        // Preconditions
        // TODO : Check for null input.

        // OPTIMIZE : Try to cast the sequence to array or list;
        // if it succeeds use the specialized method for that type for better performance.
        HashMap (PatriciaHashMap.OfSeq elements)

    /// The internal representation of the HashMap.
    member private __.Trie
        with get () = trie

    //
    static member private Equals (left : HashMap<'Key, 'T>, right : HashMap<'Key, 'T>) =
        Unchecked.equals left.Trie right.Trie

    //
    static member private Compare (left : HashMap<'Key, 'T>, right : HashMap<'Key, 'T>) =
        Unchecked.compare left.Trie right.Trie

    /// <inherit />
    override __.Equals other =
        match other with
        | :? HashMap<'Key, 'T> as other ->
            Unchecked.equals trie other.Trie
        | _ ->
            false

    /// <inherit />
    override __.GetHashCode () =
        Unchecked.hash trie

    //
    member __.Item
        with get key =
            let keyHash = uint32 <| hash key
            match PatriciaHashMap.TryFind (keyHash, key, trie) with
            | Some v -> v
            | None ->
                keyNotFound "The map does not contain a binding for the specified key."

    /// The number of bindings in the HashMap.
    member __.Count
        with get () : int =
            PatriciaHashMap.Count trie

    /// Is the map empty?
    member __.IsEmpty
        with get () =
            match trie with
            | Empty -> true
            | _ -> false

    /// Look up an element in the HashMap returning a Some value if the
    /// element is in the domain of the HashMap and None if not.
    member __.TryFind (key : 'Key) : 'T option =
        let keyHash = uint32 <| hash key
        PatriciaHashMap.TryFind (keyHash, key, trie)

    /// Look up an element in the HashMap, raising KeyNotFoundException
    /// if no binding exists in the HashMap.
    member __.Find (key : 'Key) : 'T =
        let keyHash = uint32 <| hash key
        match PatriciaHashMap.TryFind (keyHash, key, trie) with
        | Some x -> x
        | None ->
            // TODO : Add a better error message which includes the key.
            //keyNotFound ""
            raise <| System.Collections.Generic.KeyNotFoundException ()

    /// Tests if an element is in the domain of the HashMap.
    member __.ContainsKey (key : 'Key) : bool =
        let keyHash = uint32 <| hash key
        PatriciaHashMap.ContainsKey (keyHash, key, trie)

    /// Returns a new HashMap with the binding added to this HashMap.
    member this.Add (key : 'Key, value : 'T) : HashMap<'Key, 'T> =
        // If the trie isn't modified, just return this HashMap instead of creating a new one.
        let keyHash = uint32 <| hash key
        let trie' = PatriciaHashMap.Add (keyHash, key, value, trie)
        if trie === trie' then this
        else HashMap (trie')

    /// Returns a new HashMap with the binding added to this HashMap.
    member this.TryAdd (key : 'Key, value : 'T) : HashMap<'Key, 'T> =
        // If the trie isn't modified, just return this HashMap instead of creating a new one.
        //let keyHash = uint32 <| hash key
        let trie' = PatriciaHashMap.TryAdd (key, value, trie)
        if trie === trie' then this
        else HashMap (trie')

    /// Removes an element from the domain of the HashMap.
    /// No exception is raised if the element is not present.
    member this.Remove (key : 'Key) : HashMap<'Key, 'T> =
        // If the trie isn't modified, just return this HashMap instead of creating a new one.
        let keyHash = uint32 <| hash key
        let trie' = PatriciaHashMap.Remove (keyHash, key, trie)
        if trie === trie' then this
        else HashMap (trie')
(*
    /// Returns a new HashMap created by merging the two specified HashMaps.
    member this.Union (otherMap : HashMap<'Key, 'T>) : HashMap<'Key, 'T> =
        // If the result is the same (physical equality) to one of the inputs,
        // return that input instead of creating a new HashMap.
        let trie' = PatriciaHashMap.Union (trie, otherMap.Trie)
        if trie === trie' then this
        elif otherMap.Trie === trie' then otherMap
        else HashMap (trie')

    /// Returns the intersection of two HashMaps.
    member this.Intersect (otherMap : HashMap<'Key, 'T>) : HashMap<'Key, 'T> =
        // If the result is the same (physical equality) to one of the inputs,
        // return that input instead of creating a new HashMap.
        let trie' = PatriciaHashMap.Intersect (trie, otherMap.Trie)
        if trie === trie' then this
        elif otherMap.Trie === trie' then otherMap
        else HashMap (trie')

    /// Returns a new HashMap created by removing the second HashMap from the first.
    member this.Difference (otherMap : HashMap<'Key, 'T>) : HashMap<'Key, 'T> =
        // If the result is the same (physical equality) to one of the inputs,
        // return that input instead of creating a new HashMap.
        let trie' = PatriciaHashMap.Difference (trie, otherMap.Trie)
        if trie === trie' then this
        elif otherMap.Trie === trie' then otherMap
        else HashMap (trie')

    /// Returns true if 'other' is a submap of this map.
    member this.IsSubmapOfBy (predicate, other : HashMap<'Key, 'T>) : bool =
        PatriciaHashMap.IsSubmapOfBy predicate other.Trie trie
*)
    /// The HashMap containing the given binding.
    static member Singleton (key : 'Key, value : 'T) : HashMap<'Key, 'T> =
        let keyHash = uint32 <| hash key
        HashMap (
            Lf (keyHash, Tail (key, value)))

    /// Returns a new HashMap made from the given bindings.
    static member OfSeq (source : seq<'Key * 'T>) : HashMap<'Key, 'T> =
        // Preconditions
        checkNonNull "source" source

        HashMap (PatriciaHashMap.OfSeq source)

    /// Returns a new HashMap made from the given bindings.
    static member OfList (source : ('Key * 'T) list) : HashMap<'Key, 'T> =
        // Preconditions
        checkNonNull "source" source

        // OPTIMIZATION : If the source is empty return immediately.
        if List.isEmpty source then
            HashMap.Empty
        else
            HashMap (PatriciaHashMap.OfList source)

    /// Returns a new HashMap made from the given bindings.
    static member OfArray (source : ('Key * 'T)[]) : HashMap<'Key, 'T> =
        // Preconditions
        checkNonNull "source" source

        // OPTIMIZATION : If the source is empty return immediately.
        if Array.isEmpty source then
            HashMap.Empty
        else
            HashMap (PatriciaHashMap.OfArray source)

//    /// Returns a new HashMap made from the given bindings.
//    static member OfMap (source : Map<'Key, 'T>) : HashMap<'Key, 'T> =
//        // Preconditions
//        checkNonNull "source" source
//
//        // OPTIMIZATION : If the source is empty return immediately.
//        if Map.isEmpty source then
//            HashMap.Empty
//        else
//            HashMap (PatriciaHashMap.OfMap source)

    //
    member __.ToSeq () =
        PatriciaHashMap.ToSeq trie

    //
    member __.ToList () : ('Key * 'T) list =
        PatriciaHashMap.FoldBack ((fun k v list -> (k, v) :: list), [], trie)

    //
    member __.ToArray () : ('Key * 'T)[] =
        let elements = ResizeArray ()
        PatriciaHashMap.Iterate (FuncConvert.FuncFromTupled<_,_,_> elements.Add, trie)
        elements.ToArray ()

//    //
//    member __.ToMap () : Map<'Key, 'T> =
//        PatriciaHashMap.FoldBack (Map.add, Map.empty, trie)

    //
    member internal __.ToKvpArray () : KeyValuePair<'Key, 'T>[] =
        let elements = ResizeArray (1024)

        PatriciaHashMap.Iterate ((fun key value ->
            elements.Add (
                KeyValuePair (key, value))), trie)

        elements.ToArray ()

    //
    member __.Iterate (action : 'Key -> 'T -> unit) : unit =
        PatriciaHashMap.Iterate (action, trie)

    //
    member __.IterateBack (action : 'Key -> 'T -> unit) : unit =
        PatriciaHashMap.IterateBack (action, trie)

    //
    member __.Fold (folder : 'State -> 'Key -> 'T -> 'State, state : 'State) : 'State =
        PatriciaHashMap.Fold (folder, state, trie)

    //
    member __.FoldBack (folder : 'Key -> 'T -> 'State -> 'State, state : 'State) : 'State =
        PatriciaHashMap.FoldBack (folder, state, trie)

    //
    member __.TryFindKey (predicate : 'Key -> 'T -> bool) : 'Key option =
        PatriciaHashMap.TryFindKey (predicate, trie)

    //
    member this.FindKey (predicate : 'Key -> 'T -> bool) : 'Key =
        match this.TryFindKey predicate with
        | Some key ->
            key
        | None ->
            // TODO : Add a better error message
            //keyNotFound ""
            raise <| System.Collections.Generic.KeyNotFoundException ()

    //
    member this.Exists (predicate : 'Key -> 'T -> bool) : bool =
        this.TryFindKey predicate
        |> Option.isSome

    //
    member this.Forall (predicate : 'Key -> 'T -> bool) : bool =
        this.TryFindKey (fun k v ->
            not <| predicate k v)
        |> Option.isNone

    //
    member __.TryPick (picker : 'Key -> 'T -> 'U option) : 'U option =
        PatriciaHashMap.TryPick (picker, trie)

    //
    member this.Pick (picker : 'Key -> 'T -> 'U option) : 'U =
        match this.TryPick picker with
        | Some value ->
            value
        | None ->
            // TODO : Add a better error message
            //keyNotFound ""
            raise <| System.Collections.Generic.KeyNotFoundException ()

    //
    member this.Choose (chooser : 'Key -> 'T -> 'U option) : HashMap<'Key, 'U> =
        let chooser = FSharpFunc<_,_,_>.Adapt chooser

        this.Fold ((fun chosenMap key value ->
            match chooser.Invoke (key, value) with
            | None ->
                chosenMap
            | Some newValue ->
                chosenMap.Add (key, newValue)), HashMap.Empty)

    //
    member this.Filter (predicate : 'Key -> 'T -> bool) : HashMap<'Key, 'T> =
        let predicate = FSharpFunc<_,_,_>.Adapt predicate

        this.Fold ((fun filteredMap key value ->
            if predicate.Invoke (key, value) then
                filteredMap
            else
                filteredMap.Remove key), this)

    (* OPTIMIZE : The methods below should be replaced with optimized implementations where possible. *)    

    //
    // OPTIMIZE : We should be able to implement an optimized version
    // of Map which builds the mapped trie from the bottom-up; this works
    // because the mapped trie will have the same structure as the original,
    // just with different values in the leaves.
    member this.Map (mapping : 'Key -> 'T -> 'U) : HashMap<'Key, 'U> =
        let mapping = FSharpFunc<_,_,_>.Adapt mapping

        this.Fold ((fun map key value ->
            map.Add (key, mapping.Invoke (key, value))), HashMap.Empty)

    //
    member this.Partition (predicate : 'Key -> 'T -> bool) : HashMap<'Key, 'T> * HashMap<'Key, 'T> =
        let predicate = FSharpFunc<_,_,_>.Adapt predicate

        this.Fold ((fun (trueMap, falseMap) key value ->
            if predicate.Invoke (key, value) then
                trueMap.Add (key, value),
                falseMap
            else
                trueMap,
                falseMap.Add (key, value)), (HashMap.Empty, HashMap.Empty))

    //
    member this.MapPartition (partitioner : 'Key -> 'T -> Choice<'U, 'V>) : HashMap<'Key, 'U> * HashMap<'Key, 'V> =
        let partitioner = FSharpFunc<_,_,_>.Adapt partitioner

        this.Fold ((fun (map1, map2) key value ->
            match partitioner.Invoke (key, value) with
            | Choice1Of2 value ->
                map1.Add (key, value),
                map2
            | Choice2Of2 value ->
                map1,
                map2.Add (key, value)), (HashMap.Empty, HashMap.Empty))

    interface System.IEquatable<HashMap<'Key, 'T>> with
        /// <inherit />
        member this.Equals other =
            HashMap<_,_>.Equals (this, other)

    interface System.IComparable with
        /// <inherit />
        member this.CompareTo other =
            HashMap<_,_>.Compare (this, other :?> HashMap<'Key, 'T>)

    interface System.IComparable<HashMap<'Key, 'T>> with
        /// <inherit />
        member this.CompareTo other =
            HashMap<_,_>.Compare (this, other)

    interface System.Collections.IEnumerable with
        /// <inherit />
        member __.GetEnumerator () =
            (PatriciaHashMap.ToSeq trie |> Seq.map (fun (k, v) ->
                System.Collections.DictionaryEntry (k, v))).GetEnumerator ()
            :> System.Collections.IEnumerator

    interface IEnumerable<KeyValuePair<'Key, 'T>> with
        /// <inherit />
        member __.GetEnumerator () =
            (PatriciaHashMap.ToSeq trie |> Seq.map (fun (k, v) ->
                KeyValuePair (k, v))).GetEnumerator ()

    interface ICollection<KeyValuePair<'Key, 'T>> with
        /// <inherit />
        member __.Count
            with get () =
                PatriciaHashMap.Count trie

        /// <inherit />
        member __.IsReadOnly
            with get () = true

        /// <inherit />
        member __.Add x =
            notSupported "HashMaps cannot be mutated."

        /// <inherit />
        member __.Clear () =
            notSupported "HashMaps cannot be mutated."

        /// <inherit />
        member __.Contains (item : KeyValuePair<'Key, 'T>) =
            let keyHash = uint32 <| hash item.Key
            match PatriciaHashMap.TryFind (keyHash, item.Key, trie) with
            | None ->
                false
            | Some value ->
                Unchecked.equals value item.Value

        /// <inherit />
        member this.CopyTo (array, arrayIndex) =
            // Preconditions
            checkNonNull "array" array
            if arrayIndex < 0 then
                raise <| System.ArgumentOutOfRangeException "arrayIndex"

            let count = PatriciaHashMap.Count trie
            if arrayIndex + count > Array.length array then
                invalidArg "arrayIndex"
                    "There is not enough room in the array to copy the \
                     elements when starting at the specified index."

            this.Fold ((fun index key value ->
                array.[index] <- KeyValuePair (key, value)
                index + 1), arrayIndex)
            |> ignore

        /// <inherit />
        member __.Remove item : bool =
            notSupported "HashMaps cannot be mutated."

    interface IDictionary<'Key, 'T> with
        /// <inherit />
        member __.Item
            with get key =
                let keyHash = uint32 <| hash key
                match PatriciaHashMap.TryFind (keyHash, key, trie) with
                | Some value ->
                    value
                | None ->
                    // TODO : Provide a better error message here.
                    //keyNotFound ""
                    raise <| System.Collections.Generic.KeyNotFoundException ()
            and set key value =
                notSupported "HashMaps cannot be mutated."

        /// <inherit />
        member __.Keys
            with get () =
                // OPTIMIZE : Change this to use HashSet instead so it'll be faster to test set membership.
                let keys = ResizeArray ()
                PatriciaHashMap.Iterate ((fun k _ -> keys.Add k), trie)
                
                System.Collections.ObjectModel.ReadOnlyCollection (keys)
                :> ICollection<'Key>

        /// <inherit />
        member __.Values
            with get () =
                // OPTIMIZE : Change this to use HashSet instead so it'll be faster to test set membership.
                let values = ResizeArray ()
                PatriciaHashMap.Iterate ((fun _ v -> values.Add v), trie)
                
                System.Collections.ObjectModel.ReadOnlyCollection (values)
                :> ICollection<'T>

        /// <inherit />
        member __.Add (key, value) =
            notSupported "HashMaps cannot be mutated."

        /// <inherit />
        member __.ContainsKey key =
            let keyHash = uint32 <| hash key
            PatriciaHashMap.ContainsKey (keyHash, key, trie)

        /// <inherit />
        member __.Remove key =
            notSupported "HashMaps cannot be mutated."

        /// <inherit />
        member __.TryGetValue (key, value) =
            let keyHash = uint32 <| hash key
            match PatriciaHashMap.TryFind (keyHash, key, trie) with
            | None ->
                false
            | Some v ->
                value <- v
                true

//
and [<Sealed>]
    internal HashMapDebuggerProxy<'Key, 'T when 'Key : equality> (map : HashMap<'Key, 'T>) =

    [<DebuggerBrowsable(DebuggerBrowsableState.RootHidden)>]
    member __.Items
        with get () : KeyValuePair<'Key, 'T>[] =
            map.ToKvpArray ()


/// Functional programming operators related to the HashMap type.
[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HashMap =
    /// The empty map.
    [<CompiledName("Empty")>]
    let empty<'Key, 'T when 'Key : equality> =
        HashMap<'Key, 'T>.Empty

    /// Is the map empty?
    [<CompiledName("IsEmpty")>]
    let inline isEmpty (map : HashMap<'Key, 'T>) : bool =
        // Preconditions
        checkNonNull "map" map

        map.IsEmpty

    /// Returns the number of bindings in the map.
    [<CompiledName("Count")>]
    let inline count (map : HashMap<'Key, 'T>) : int =
        // Preconditions
        checkNonNull "map" map
        
        map.Count

    /// The HashMap containing the given binding.
    [<CompiledName("Singleton")>]
    let inline singleton (key : 'Key) (value : 'T) : HashMap<'Key, 'T> =
        HashMap.Singleton (key, value)

    /// Tests if an element is in the domain of the map.
    [<CompiledName("ContainsKey")>]
    let inline containsKey (key : 'Key) (map : HashMap<'Key, 'T>) : bool =
        // Preconditions
        checkNonNull "map" map

        map.ContainsKey key

    /// Look up an element in the map returning a Some value if the
    /// element is in the domain of the map and None if not.
    [<CompiledName("TryFind")>]
    let inline tryFind (key : 'Key) (map : HashMap<'Key, 'T>) : 'T option =
        // Preconditions
        checkNonNull "map" map
        
        map.TryFind key
        
    /// Look up an element in the map, raising KeyNotFoundException
    /// if the map does not contain a binding for the key.
    [<CompiledName("Find")>]
    let inline find (key : 'Key) (map : HashMap<'Key, 'T>) : 'T =
        // Preconditions
        checkNonNull "map" map

        map.Find key

    /// Look up an element in the map, returning the given default value
    /// if the map does not contain a binding for the key.
    [<CompiledName("FindOrDefault")>]
    let inline findOrDefault defaultValue (key : 'Key) (map : HashMap<'Key, 'T>) =
        defaultArg (map.TryFind key) defaultValue

    /// Returns the key of the first mapping in the collection which satisfies the given
    /// predicate. Returns None if no such mapping is found.
    [<CompiledName("TryFindKey")>]
    let inline tryFindKey (predicate : 'Key -> 'T -> bool) (map : HashMap<'Key, 'T>) : 'Key option =
        // Preconditions
        checkNonNull "map" map
        
        map.TryFindKey predicate

    //
    [<CompiledName("FindKey")>]
    let inline findKey (predicate : 'Key -> 'T -> bool) (map : HashMap<'Key, 'T>) : 'Key =
        // Preconditions
        checkNonNull "map" map

        map.FindKey predicate

    /// Returns a new map with the binding added to this map.
    [<CompiledName("Add")>]
    let inline add (key : 'Key) (value : 'T) (map : HashMap<'Key, 'T>) : HashMap<'Key, 'T> =
        // Preconditions
        checkNonNull "map" map

        map.Add (key, value)

    //
    [<CompiledName("TryAdd")>]
    let inline tryAdd (key : 'Key) (value : 'T) (map : HashMap<'Key, 'T>) : HashMap<'Key, 'T> =
        // Preconditions
        checkNonNull "map" map

        map.TryAdd (key, value)

    /// Removes an element from the domain of the map.
    /// No exception is raised if the element is not present.
    [<CompiledName("Remove")>]
    let inline remove (key : 'Key) (map : HashMap<'Key, 'T>) : HashMap<'Key, 'T> =
        // Preconditions
        checkNonNull "map" map

        map.Remove key
(*
    /// Returns a new map created by merging the two specified maps.
    [<CompiledName("Union")>]
    let inline union (map1 : HashMap<'Key, 'T>) (map2 : HashMap<'Key, 'T>) : HashMap<'Key, 'T> =
        // Preconditions
        checkNonNull "map1" map1
        checkNonNull "map2" map2

        map1.Union map2

    /// Returns the intersection of two maps.
    [<CompiledName("Intersect")>]
    let inline intersect (map1 : HashMap<'Key, 'T>) (map2 : HashMap<'Key, 'T>) : HashMap<'Key, 'T> =
        // Preconditions
        checkNonNull "map1" map1
        checkNonNull "map2" map2

        map1.Intersect map2

    /// Returns a new map created by removing the second map from the first.
    [<CompiledName("Difference")>]
    let inline difference (map1 : HashMap<'Key, 'T>) (map2 : HashMap<'Key, 'T>) : HashMap<'Key, 'T> =
        // Preconditions
        checkNonNull "map1" map1
        checkNonNull "map2" map2

        map1.Difference map2
*)
    /// Returns a new map made from the given bindings.
    [<CompiledName("OfSeq")>]
    let inline ofSeq source : HashMap<'Key, 'T> =
        // Preconditions are checked by the member.
        HashMap.OfSeq source

    /// Returns a new map made from the given bindings.
    [<CompiledName("OfList")>]
    let inline ofList source : HashMap<'Key, 'T> =
        // Preconditions are checked by the member.
        HashMap.OfList source

    /// Returns a new map made from the given bindings.
    [<CompiledName("OfArray")>]
    let inline ofArray source : HashMap<'Key, 'T> =
        // Preconditions are checked by the member.
        HashMap.OfArray source
(*
    /// Returns a new map made from the given bindings.
    [<CompiledName("OfMap")>]
    let inline ofMap source : HashMap<'Key, 'T> =
        // Preconditions are checked by the member.
        HashMap.OfMap source
*)
    /// Views the collection as an enumerable sequence of pairs.
    /// The sequence will be ordered by the keys of the map.
    [<CompiledName("ToSeq")>]
    let inline toSeq (map : HashMap<'Key, 'T>) =
        // Preconditions
        checkNonNull "map" map
        
        map.ToSeq ()

    /// Returns a list of all key-value pairs in the mapping.
    /// The list will be ordered by the keys of the map.
    [<CompiledName("ToList")>]
    let inline toList (map : HashMap<'Key, 'T>) =
        // Preconditions
        checkNonNull "map" map
        
        map.ToList ()

    /// Returns an array of all key-value pairs in the mapping.
    /// The list will be ordered by the keys of the map.
    [<CompiledName("ToArray")>]
    let inline toArray (map : HashMap<'Key, 'T>) =
        // Preconditions
        checkNonNull "map" map

        map.ToArray ()
(*
    /// Returns a new Map created from the given HashMap.
    [<CompiledName("ToMap")>]
    let inline toMap (map : HashMap<'Key, 'T>) =
        // Preconditions
        checkNonNull "map" map

        map.ToMap ()
*)
    /// Searches the map looking for the first element where the given function
    /// returns a Some value. If no such element is found, returns None.
    [<CompiledName("TryPick")>]
    let inline tryPick (picker : 'Key -> 'T -> 'U option) (map : HashMap<'Key, 'T>) : 'U option =
        // Preconditions
        checkNonNull "map" map
        
        map.TryPick picker

    /// Searches the map looking for the first element where the given function
    /// returns a Some value.
    [<CompiledName("Pick")>]
    let inline pick (picker : 'Key -> 'T -> 'U option) (map : HashMap<'Key, 'T>) : 'U =
        // Preconditions
        checkNonNull "map" map

        map.Pick picker

    /// Builds a new collection whose elements are the results of applying the given function
    /// to each of the elements of the collection. The key passed to the function indicates
    /// the key of the element being transformed.
    [<CompiledName("Map")>]
    let inline map (mapping : 'Key -> 'T -> 'U) (map : HashMap<'Key, 'T>) : HashMap<'Key, 'U> =
        // Preconditions
        checkNonNull "map" map
        
        map.Map mapping

    /// <summary>
    /// Builds a new map containing only the bindings for which the given
    /// predicate returns &quot;true&quot;.
    /// </summary>
    [<CompiledName("Filter")>]
    let inline filter (predicate : 'Key -> 'T -> bool) (map : HashMap<'Key, 'T>) : HashMap<'Key, 'T> =
        // Preconditions
        checkNonNull "map" map
        
        map.Filter predicate

    /// <summary>
    /// Applies the given function to each binding in the map.
    /// Returns the map comprised of the results "x" for each binding
    /// where the function returns <c>Some(x)</c>.
    /// </summary>
    [<CompiledName("Choose")>]
    let inline choose (chooser : 'Key -> 'T -> 'U option) (map : HashMap<'Key, 'T>) : HashMap<'Key, 'U> =
        // Preconditions
        checkNonNull "map" map
        
        map.Choose chooser

    /// Applies the given function to each binding in the map.
    [<CompiledName("Iterate")>]
    let inline iter (action : 'Key -> 'T -> unit) (map : HashMap<'Key, 'T>) : unit =
        // Preconditions
        checkNonNull "map" map
        
        map.Iterate action

    /// Applies the given function to each binding in the map.
    [<CompiledName("IterateBack")>]
    let inline iterBack (action : 'Key -> 'T -> unit) (map : HashMap<'Key, 'T>) : unit =
        // Preconditions
        checkNonNull "map" map

        map.IterateBack action

    /// Folds over the bindings in the map.
    [<CompiledName("Fold")>]
    let inline fold (folder : 'State -> 'Key -> 'T -> 'State)
            (state : 'State) (map : HashMap<'Key, 'T>) : 'State =
        // Preconditions
        checkNonNull "map" map
        
        map.Fold (folder, state)

    /// Folds over the bindings in the map.
    [<CompiledName("FoldBack")>]
    let inline foldBack
            (folder : 'Key -> 'T -> 'State -> 'State) (map : HashMap<'Key, 'T>) (state : 'State) : 'State =
        // Preconditions
        checkNonNull "map" map

        map.FoldBack (folder, state)

    /// Determines if any binding in the map matches the specified predicate.
    [<CompiledName("Exists")>]
    let inline exists (predicate : 'Key -> 'T -> bool) (map : HashMap<'Key, 'T>) : bool =
        // Preconditions
        checkNonNull "map" map
        
        map.Exists predicate

    /// Determines if all bindings in the map match the specified predicate.
    [<CompiledName("Forall")>]
    let inline forall (predicate : 'Key -> 'T -> bool) (map : HashMap<'Key, 'T>) : bool =
        // Preconditions
        checkNonNull "map" map

        map.Forall predicate

    /// Splits the map into two maps containing the bindings for which the given
    /// predicate returns true and false, respectively.
    [<CompiledName("Partition")>]
    let inline partition (predicate : 'Key -> 'T -> bool) (map : HashMap<'Key, 'T>) : HashMap<'Key, 'T> * HashMap<'Key, 'T> =
        // Preconditions
        checkNonNull "map" map
        
        map.Partition predicate

    /// Splits the map into two maps by applying the given partitioning function
    /// to each binding in the map.
    [<CompiledName("MapPartition")>]
    let inline mapPartition (partitioner : 'Key -> 'T -> Choice<'U, 'V>)
            (map : HashMap<'Key, 'T>) : HashMap<'Key, 'U> * HashMap<'Key, 'V> =
        // Preconditions
        checkNonNull "map" map

        map.MapPartition partitioner

