﻿(*

Copyright 2005-2009 Microsoft Corporation
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

/// Functional operators related to the System.Collections.Generic.List&lt;T&gt;
/// type (called ResizeArray in F#).
[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ExtCore.Collections.ResizeArray

open System.Collections.Generic
open LanguagePrimitives
open OptimizedClosures
open ExtCore


/// Return the length of the collection.
[<CompiledName("Length")>]
let inline length (resizeArray : ResizeArray<'T>) =
    resizeArray.Count

//
[<CompiledName("IsEmpty")>]
let inline isEmpty (resizeArray : ResizeArray<'T>) =
    resizeArray.Count = 0

/// Fetch an element from the collection.
[<CompiledName("Get")>]
let inline get (resizeArray : ResizeArray<'T>) index =
    resizeArray.[index]

/// Set the value of an element in the collection.
[<CompiledName("Set")>]
let inline set (resizeArray : ResizeArray<'T>) index value =
    resizeArray.[index] <- value

/// Create an array whose elements are all initially the given value.
[<CompiledName("Create")>]
let create count value : ResizeArray<'T> =
    // Preconditions
    if count < 0 then
        invalidArg "count" "The number of elements may not be negative."

    let resizeArray = ResizeArray (count)
    for i = 0 to count - 1 do
        resizeArray.Add value
    resizeArray

/// Create an array by calling the given generator on each index.
[<CompiledName("Init")>]
let init count initializer : ResizeArray<'T> =
    // Preconditions
    if count < 0 then
        invalidArg "count" "The number of elements may not be negative."

    let resizeArray = ResizeArray (count)
    for i = 0 to count - 1 do
        resizeArray.Add <| initializer count
    resizeArray

//
[<CompiledName("Add")>]
let inline add item (resizeArray : ResizeArray<'T>) =
    resizeArray.Add item

//
[<CompiledName("Contains")>]
let inline contains (value : 'T) (resizeArray : ResizeArray<'T>) =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    resizeArray.Contains value

//
[<CompiledName("OfSeq")>]
let inline ofSeq (sequence : seq<'T>) : ResizeArray<'T> =
    ResizeArray (sequence)

//
[<CompiledName("ToSeq")>]
let toSeq (resizeArray : ResizeArray<'T>) : seq<'T> =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    Seq.readonly resizeArray

//
[<CompiledName("OfList")>]
let ofList (list : 'T list) : ResizeArray<'T> =
    // Preconditions
    checkNonNull "list" list

    let len = list.Length
    let res = ResizeArray<_>(len)
    let rec add = function
        | [] -> ()
        | e::l -> res.Add(e); add l
    add list
    res

//
[<CompiledName("ToList")>]
let toList (resizeArray : ResizeArray<'T>) : 'T list =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    let mutable res = []
    for i = length resizeArray - 1 downto 0 do
        res <- resizeArray.[i] :: res
    res

//
[<CompiledName("OfArray")>]
let inline ofArray (arr : 'T[]) : ResizeArray<'T> =
    ResizeArray (arr)

//
[<CompiledName("ToArray")>]
let inline toArray (resizeArray : ResizeArray<'T>) =
    resizeArray.ToArray ()

//
[<CompiledName("SortInPlace")>]
let inline sortInPlace<'T when 'T : comparison> (resizeArray : ResizeArray<'T>) =
    resizeArray.Sort ()
        
//
[<CompiledName("SortInPlaceBy")>]
let inline sortInPlaceBy<'T, 'Key when 'Key : comparison>
        (projection : 'T -> 'Key) (resizeArray : ResizeArray<'T>) =
    resizeArray.Sort (fun x y ->
        compare (projection x) (projection y))

//
[<CompiledName("SortInPlaceWith")>]
let inline sortInPlaceWith (comparer : 'T -> 'T -> int) (resizeArray : ResizeArray<'T>) =
    resizeArray.Sort (comparer)

//
[<CompiledName("Copy")>]
let inline copy (resizeArray : ResizeArray<'T>) : ResizeArray<'T> =
    ResizeArray (resizeArray)

//
[<CompiledName("Singleton")>]
let singleton value : ResizeArray<'T> =
    let resizeArray = ResizeArray ()
    resizeArray.Add value
    resizeArray

/// Build a new array that contains the elements of each of the given sequence of arrays.
[<CompiledName("Concat")>]
let concat (resizeArrays : seq<ResizeArray<'T>>) : ResizeArray<'T> =
    // Preconditions
    checkNonNull "resizeArrays" resizeArrays

    let flattened = ResizeArray ()
    for resizeArray in resizeArrays do
        flattened.AddRange resizeArray
    flattened
    
/// Build a new array that contains the elements of the first array followed by
/// the elements of the second array.
[<CompiledName("Append")>]
let append (resizeArray1 : ResizeArray<'T>) (resizeArray2 : ResizeArray<'T>) : ResizeArray<'T> =
    // Preconditions
    checkNonNull "resizeArray1" resizeArray1
    checkNonNull "resizeArray2" resizeArray2

    let combined = ResizeArray (resizeArray1.Count + resizeArray2.Count)
    combined.AddRange resizeArray1
    combined.AddRange resizeArray2
    combined

//
[<CompiledName("Sub")>]
let sub (resizeArray : ResizeArray<'T>) start count : ResizeArray<'T> =
    // Preconditions
    checkNonNull "resizeArray" resizeArray
    if start < 0 then
        invalidArg "start" "The start index cannot be less than zero (0)."
    elif count < 0 then
        invalidArg "count" "The number of elements to copy cannot be less than zero (0)."
    elif start + count > length resizeArray then
        invalidArg "count" "There are fewer than 'count' elements between the 'start' index and the end of the collection."
    
    resizeArray.GetRange (start, count)

//
[<CompiledName("Fill")>]
let fill (resizeArray : ResizeArray<'T>) start count value : unit =
    // Preconditions
    checkNonNull "resizeArray" resizeArray
    if start < 0 then
        invalidArg "start" "The start index cannot be less than zero (0)."
    elif count < 0 then
        invalidArg "count" "The number of elements to copy cannot be less than zero (0)."
    elif start + count > length resizeArray then
        invalidArg "count" "There are fewer than 'count' elements between the 'start' index and the end of the collection."
    
    // Overwrite the items within the range using the specified value.
    for i = start to start + count - 1 do
        resizeArray.[i] <- value

//
[<CompiledName("Rev")>]
let rev (resizeArray : ResizeArray<'T>) : ResizeArray<'T> =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    let len = length resizeArray
    let result = ResizeArray (len)
    for i = len - 1 downto 0 do
        result.Add resizeArray.[i]
    result

//
[<CompiledName("Blit")>]
let blit (source : ResizeArray<'T>) sourceIndex (target : ResizeArray<'T>) targetIndex count : unit =
    // Preconditions
    checkNonNull "source" source
    checkNonNull "target" target
    if sourceIndex < 0 then
        invalidArg "sourceIndex" "The source index cannot be negative."
    elif targetIndex < 0 then
        invalidArg "targetIndex" "The target index cannot be negative."
    elif count < 0 then
        invalidArg "count" "Cannot copy a negative number of items."
    elif sourceIndex + count > length source then
        invalidArg "sourceIndex" "There are fewer than 'count' elements between 'sourceIndex' and the end of the source ResizeArray."
    elif targetIndex + count > length target then
        invalidArg "sourceIndex" "There are fewer than 'count' elements between 'sourceIndex' and the end of the target ResizeArray."

    for i = 0 to count - 1 do
        target.[targetIndex + i] <- source.[sourceIndex + i]

//
[<CompiledName("Zip")>]
let zip (resizeArray1 : ResizeArray<'T1>) (resizeArray2 : ResizeArray<'T2>)
    : ResizeArray<'T1 * 'T2> =
    // Preconditions
    checkNonNull "resizeArray1" resizeArray1
    checkNonNull "resizeArray2" resizeArray2

    let len = length resizeArray1
    if len <> length resizeArray2 then
        invalidArg "resizeArray2" "The ResizeArrays have different lengths."

    let results = ResizeArray (len)
    for i = 0 to len - 1 do
        results.Add (resizeArray1.[i], resizeArray2.[i])
    results

//
[<CompiledName("Unzip")>]
let unzip (resizeArray : ResizeArray<'T1 * 'T2>) : ResizeArray<'T1> * ResizeArray<'T2> =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    let len = length resizeArray
    let results1 = ResizeArray (len)
    let results2 = ResizeArray (len)

    for i = 0 to len - 1 do
        let x, y = resizeArray.[i]
        results1.Add x
        results2.Add y

    results1, results2

//
[<CompiledName("Exists")>]
let inline exists (predicate : 'T -> bool) (resizeArray : ResizeArray<'T>) : bool =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    resizeArray.Exists (System.Predicate predicate)

//
[<CompiledName("Exists2")>]
let exists2 predicate (resizeArray1 : ResizeArray<'T1>) (resizeArray2 : ResizeArray<'T2>) : bool =
    // Preconditions
    checkNonNull "resizeArray1" resizeArray1
    checkNonNull "resizeArray2" resizeArray2

    let len = length resizeArray1
    if len <> length resizeArray2 then
        invalidArg "resizeArray2" "The ResizeArrays have different lengths."

    let predicate = FSharpFunc<_,_,_>.Adapt predicate
    
    let mutable index = 0
    let mutable foundMatch = false
    while index < len && not foundMatch do
        foundMatch <- predicate.Invoke (resizeArray1.[index], resizeArray2.[index])
        index <- index + 1
    foundMatch

//
[<CompiledName("Forall")>]
let inline forall (predicate : 'T -> bool) (resizeArray : ResizeArray<'T>) : bool =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    resizeArray.TrueForAll (System.Predicate predicate)

//
[<CompiledName("Forall2")>]
let forall2 predicate (resizeArray1 : ResizeArray<'T1>) (resizeArray2 : ResizeArray<'T2>) : bool =
    // Preconditions
    checkNonNull "resizeArray1" resizeArray1
    checkNonNull "resizeArray2" resizeArray2

    let len = length resizeArray1
    if len <> length resizeArray2 then
        invalidArg "resizeArray2" "The ResizeArrays have different lengths."

    let predicate = FSharpFunc<_,_,_>.Adapt predicate
    
    let mutable index = 0
    let mutable allMatch = true
    while index < len && allMatch do
        allMatch <- predicate.Invoke (resizeArray1.[index], resizeArray2.[index])
        index <- index + 1
    allMatch

//
[<CompiledName("Filter")>]
let inline filter (predicate : 'T -> bool) (resizeArray : ResizeArray<'T>) : ResizeArray<'T> =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    resizeArray.FindAll (System.Predicate predicate)

//
[<CompiledName("Choose")>]
let choose (chooser : 'T -> 'U option) (resizeArray : ResizeArray<'T>) : ResizeArray<'U> =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    // OPTIMIZATION : If the input list is empty return immediately.
    if isEmpty resizeArray then
        ResizeArray ()
    else
        let result = ResizeArray ()
        let count = resizeArray.Count

        for i = 0 to count - 1 do
            match chooser resizeArray.[i] with
            | None -> ()
            | Some value ->
                result.Add value

        result

//
[<CompiledName("TryFind")>]
let tryFind (predicate : 'T -> bool) (resizeArray : ResizeArray<'T>) : 'T option =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    match resizeArray.FindIndex (System.Predicate predicate) with
    | -1 ->
        None
    | index ->
        Some resizeArray.[index]

//
[<CompiledName("Find")>]
let find (predicate : 'T -> bool) (resizeArray : ResizeArray<'T>) : 'T =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    match resizeArray.FindIndex (System.Predicate predicate) with
    | -1 ->
        // TODO : Add a better error message.
        // keyNotFound ""
        raise <| System.Collections.Generic.KeyNotFoundException ()
    | index ->
        resizeArray.[index]

//
[<CompiledName("TryFindIndex")>]
let tryFindIndex (predicate : 'T -> bool) (resizeArray : ResizeArray<'T>) : int option =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    match resizeArray.FindIndex (System.Predicate predicate) with
    | -1 ->
        None
    | index ->
        Some index
        
//
[<CompiledName("FindIndex")>]
let findIndex (predicate : 'T -> bool) (resizeArray : ResizeArray<'T>) : int =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    match resizeArray.FindIndex (System.Predicate predicate) with
    | -1 ->
        // TODO : Add a better error message.
        // keyNotFound ""
        raise <| System.Collections.Generic.KeyNotFoundException ()
    | index ->
        index

//
[<CompiledName("TryFindIndexIndexed")>]
let tryFindIndexi predicate (resizeArray : ResizeArray<'T>) : int option =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    let predicate = FSharpFunc<_,_,_>.Adapt predicate

    let lastIndex = length resizeArray - 1
    let mutable index = -1
    let mutable foundMatch = false
    while index < lastIndex && not foundMatch do
        let i = index + 1
        index <- i
        foundMatch <- predicate.Invoke (i, resizeArray.[i])

    if foundMatch then
        Some index
    else None

//
[<CompiledName("FindIndexIndexed")>]
let findIndexi predicate (resizeArray : ResizeArray<'T>) : int =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    match tryFindIndexi predicate resizeArray with
    | Some index ->
        index
    | None ->
        keyNotFound "An element satisfying the predicate was not found in the collection."

//
[<CompiledName("TryPick")>]
let tryPick (picker : 'T -> 'U option) (resizeArray : ResizeArray<'T>) : 'U option =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    let count = resizeArray.Count
    let mutable result = None
    let mutable index = 0

    while index < count && Option.isNone result do
        result <- picker resizeArray.[index]
        index <- index + 1
    result

//
[<CompiledName("Pick")>]
let pick (picker : 'T -> 'U option) (resizeArray : ResizeArray<'T>) : 'U =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    let count = resizeArray.Count
    let mutable result = None
    let mutable index = 0

    while index < count && Option.isNone result do
        result <- picker resizeArray.[index]
        index <- index + 1

    match result with
    | Some result ->
        result
    | None ->
        // TODO : Return a better error message
        //keyNotFound ""
        raise <| System.Collections.Generic.KeyNotFoundException ()

//
[<CompiledName("Iter")>]
let iter (action : 'T -> unit) (resizeArray : ResizeArray<'T>) =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    let count = resizeArray.Count
    for i = 0 to count - 1 do
        action resizeArray.[i]

//
[<CompiledName("IterIndexed")>]
let iteri (action : int -> 'T -> unit) (resizeArray : ResizeArray<'T>) =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    let action = FSharpFunc<_,_,_>.Adapt action

    let count = resizeArray.Count
    for i = 0 to count - 1 do
        action.Invoke (i, resizeArray.[i])

//
[<CompiledName("Iter2")>]
let iter2 action (resizeArray1 : ResizeArray<'T1>) (resizeArray2 : ResizeArray<'T1>) : unit =
    // Preconditions
    checkNonNull "resizeArray1" resizeArray1
    checkNonNull "resizeArray2" resizeArray2

    let len = length resizeArray1
    if len <> length resizeArray2 then
        invalidArg "resizeArray2" "The ResizeArrays have different lengths."

    let action = FSharpFunc<_,_,_>.Adapt action

    for i = 0 to len - 1 do
        action.Invoke (resizeArray1.[i], resizeArray2.[i])

//
[<CompiledName("IterIndexed2")>]
let iteri2 action (resizeArray1 : ResizeArray<'T1>) (resizeArray2 : ResizeArray<'T2>) : unit =
    // Preconditions
    checkNonNull "resizeArray1" resizeArray1
    checkNonNull "resizeArray2" resizeArray2

    let len = length resizeArray1
    if len <> length resizeArray2 then
        invalidArg "resizeArray2" "The ResizeArrays have different lengths."

    let action = FSharpFunc<_,_,_,_>.Adapt action

    for i = 0 to len - 1 do
        action.Invoke (i, resizeArray1.[i], resizeArray2.[i])

//
[<CompiledName("Map")>]
let inline map (mapping : 'T -> 'U) (resizeArray : ResizeArray<'T>) =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    resizeArray.ConvertAll (System.Converter mapping)

//
[<CompiledName("MapIndexed")>]
let mapi (mapping : int -> 'T -> 'U) (resizeArray : ResizeArray<'T>) =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    let mapping = FSharpFunc<_,_,_>.Adapt mapping
    let count = resizeArray.Count
    let result = ResizeArray (count)

    for i = 0 to count - 1 do
        result.Add <| mapping.Invoke (i, resizeArray.[i])
    result

//
[<CompiledName("Map2")>]
let map2 mapping (resizeArray1 : ResizeArray<'T1>) (resizeArray2 : ResizeArray<'T2>)
    : ResizeArray<'U> =
    // Preconditions
    checkNonNull "resizeArray1" resizeArray1
    checkNonNull "resizeArray2" resizeArray2

    let len = length resizeArray1
    if len <> length resizeArray2 then
        invalidArg "resizeArray2" "The ResizeArrays have different lengths."

    let mapping = FSharpFunc<_,_,_>.Adapt mapping
    let results = ResizeArray (len)

    for i = 0 to len - 1 do
        mapping.Invoke (resizeArray1.[i], resizeArray2.[i])
        |> results.Add
    results

//
[<CompiledName("MapIndexed2")>]
let mapi2 mapping (resizeArray1 : ResizeArray<'T1>) (resizeArray2 : ResizeArray<'T2>)
    : ResizeArray<'U> =
    // Preconditions
    checkNonNull "resizeArray1" resizeArray1
    checkNonNull "resizeArray2" resizeArray2

    let len = length resizeArray1
    if len <> length resizeArray2 then
        invalidArg "resizeArray2" "The ResizeArrays have different lengths."

    let mapping = FSharpFunc<_,_,_,_>.Adapt mapping
    let results = ResizeArray (len)

    for i = 0 to len - 1 do
        mapping.Invoke (i, resizeArray1.[i], resizeArray2.[i])
        |> results.Add
    results

//
[<CompiledName("Fold")>]
let fold (folder : 'State -> 'T -> 'State) state (resizeArray : ResizeArray<'T>) =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    let folder = FSharpFunc<_,_,_>.Adapt folder

    let mutable state = state
    let count = resizeArray.Count
    for i = 0 to count - 1 do
        state <- folder.Invoke (state, resizeArray.[i])
    state

//
[<CompiledName("FoldSub")>]
let foldSub folder (state : 'State) (resizeArray : ResizeArray<'T>) startIndex endIndex : 'State =
    // Preconditions
    checkNonNull "resizeArray" resizeArray
    if startIndex < 0 then
        argOutOfRange "startIndex" "The starting index cannot be negative."
    elif endIndex > 0 then
        argOutOfRange "endIndex" "The ending index cannot be negative."
    
    let len = length resizeArray
    if startIndex >= len then
        argOutOfRange "startIndex" "The starting index is outside the bounds of the ResizeArray."
    elif endIndex >= len then
        argOutOfRange "endIndex" "The ending index is outside the bounds of the ResizeArray."

    let folder = FSharpFunc<_,_,_>.Adapt folder

    // Fold over the specified range of items.
    let mutable state = state
    for i = startIndex to endIndex do
        state <- folder.Invoke (state, resizeArray.[i])
    state

//
[<CompiledName("FoldIndexed")>]
let foldi (folder : int -> 'State -> 'T -> 'State) state (resizeArray : ResizeArray<'T>) =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    let folder = FSharpFunc<_,_,_,_>.Adapt folder

    let mutable state = state
    let count = resizeArray.Count
    for i = 0 to count - 1 do
        state <- folder.Invoke (i, state, resizeArray.[i])
    state

//
[<CompiledName("Fold2")>]
let fold2 folder (state : 'State)
    (resizeArray1 : ResizeArray<'T1>) (resizeArray2 : ResizeArray<'T2>) : 'State =
    // Preconditions
    checkNonNull "resizeArray1" resizeArray1
    checkNonNull "resizeArray2" resizeArray2

    let len = length resizeArray1
    if len <> length resizeArray2 then
        invalidArg "resizeArray2" "The arrays have different lengths."

    let folder = FSharpFunc<_,_,_,_>.Adapt folder
    let mutable state = state
    for i = 0 to len - 1 do
        state <- folder.Invoke (state, resizeArray1.[i], resizeArray2.[i])
    state

//
[<CompiledName("FoldBack")>]
let foldBack (folder : 'T -> 'State -> 'State) (resizeArray : ResizeArray<'T>) state =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    let folder = FSharpFunc<_,_,_>.Adapt folder

    let mutable state = state
    for i = resizeArray.Count - 1 downto 0 do
        state <- folder.Invoke (resizeArray.[i], state)
    state

//
[<CompiledName("FoldBackSub")>]
let foldBackSub folder (resizeArray : ResizeArray<'T>) startIndex endIndex (state : 'State) : 'State =
    // Preconditions
    checkNonNull "resizeArray" resizeArray
    if startIndex < 0 then
        argOutOfRange "startIndex" "The starting index cannot be negative."
    elif endIndex > 0 then
        argOutOfRange "endIndex" "The ending index cannot be negative."
    
    let len = length resizeArray
    if startIndex >= len then
        argOutOfRange "startIndex" "The starting index is outside the bounds of the ResizeArray."
    elif endIndex >= len then
        argOutOfRange "endIndex" "The ending index is outside the bounds of the ResizeArray."

    let folder = FSharpFunc<_,_,_>.Adapt folder

    // Fold over the specified range of items.
    let mutable state = state
    for i = endIndex downto startIndex do
        state <- folder.Invoke (resizeArray.[i], state)
    state

//
[<CompiledName("FoldBackIndexed")>]
let foldiBack (folder : int -> 'T -> 'State -> 'State) (resizeArray : ResizeArray<'T>) state =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    let folder = FSharpFunc<_,_,_,_>.Adapt folder

    let mutable state = state
    for i = resizeArray.Count - 1 downto 0 do
        state <- folder.Invoke (i, resizeArray.[i], state)
    state

//
[<CompiledName("FoldBack2")>]
let foldBack2 folder (resizeArray1 : ResizeArray<'T1>)
    (resizeArray2 : ResizeArray<'T2>) (state : 'State) : 'State =
    // Preconditions
    checkNonNull "resizeArray1" resizeArray1
    checkNonNull "resizeArray2" resizeArray2

    let len = length resizeArray1
    if len <> length resizeArray2 then
        invalidArg "resizeArray2" "The arrays have different lengths."

    let folder = FSharpFunc<_,_,_,_>.Adapt folder
    let mutable state = state
    for i = len - 1 downto 0 do
        state <- folder.Invoke (resizeArray1.[i], resizeArray2.[i], state)
    state

//
[<CompiledName("Reduce")>]
let reduce (reduction : 'T -> 'T -> 'T) (resizeArray : ResizeArray<'T>) =
    // Preconditions
    checkNonNull "resizeArray" resizeArray
    if isEmpty resizeArray then
        invalidArg "resizeArray" "The ResizeArray is empty."

    let reduction = FSharpFunc<_,_,_>.Adapt reduction

    let mutable state = resizeArray.[0]
    let count = resizeArray.Count
    for i = 1 to count - 1 do
        state <- reduction.Invoke (state, resizeArray.[i])
    state

//
[<CompiledName("ReduceBack")>]
let reduceBack (reduction : 'T -> 'T -> 'T) (resizeArray : ResizeArray<'T>) : 'T =
    // Preconditions
    checkNonNull "resizeArray" resizeArray
    if isEmpty resizeArray then
        invalidArg "resizeArray" "The ResizeArray is empty."

    let reduction = FSharpFunc<_,_,_>.Adapt reduction

    let count = resizeArray.Count
    let mutable state = resizeArray.[count - 1]

    for i = count - 2 downto 0 do
        state <- reduction.Invoke (resizeArray.[i], state)
    state

//
[<CompiledName("ScanSub")>]
let scanSub folder (state : 'State) (resizeArray : ResizeArray<'T>) startIndex endIndex : ResizeArray<'State> =
    // Preconditions
    checkNonNull "resizeArray" resizeArray
    if startIndex < 0 then
        argOutOfRange "startIndex" "The starting index cannot be negative."
    elif endIndex < 0 then
        argOutOfRange "endIndex" "The ending index cannot be negative."
    
    let len = length resizeArray
    if startIndex >= len then
        argOutOfRange "startIndex" "The starting index is outside the bounds of the ResizeArray."
    elif endIndex >= len then
        argOutOfRange "endIndex" "The ending index is outside the bounds of the ResizeArray."

    let folder = FSharpFunc<_,_,_>.Adapt folder

    // Holds the initial and intermediate state values.
    let results = ResizeArray (endIndex - startIndex + 2)
    results.Add state

    // Fold over the specified range of items.
    let mutable state = state
    for i = startIndex to endIndex do
        state <- folder.Invoke (state, resizeArray.[i])
        results.Add state
    results

//
[<CompiledName("Scan")>]
let scan folder (state : 'State) (resizeArray : ResizeArray<'T>) : ResizeArray<'State> =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    scanSub folder state resizeArray 0 (length resizeArray - 1)

//
[<CompiledName("ScanBackSub")>]
let scanBackSub folder (resizeArray : ResizeArray<'T>) startIndex endIndex (state : 'State) : ResizeArray<'State> =
    // Preconditions
    checkNonNull "resizeArray" resizeArray
    if startIndex < 0 then
        argOutOfRange "startIndex" "The starting index cannot be negative."
    elif endIndex < 0 then
        argOutOfRange "endIndex" "The ending index cannot be negative."
    
    let len = length resizeArray
    if startIndex >= len then
        argOutOfRange "startIndex" "The starting index is outside the bounds of the ResizeArray."
    elif endIndex >= len then
        argOutOfRange "endIndex" "The ending index is outside the bounds of the ResizeArray."

    let folder = FSharpFunc<_,_,_>.Adapt folder

    // Holds the initial and intermediate state values.
    let results = ResizeArray (endIndex - startIndex + 2)
    results.Add state

    // Fold over the specified range of items.
    let mutable state = state
    for i = endIndex downto startIndex do
        state <- folder.Invoke (resizeArray.[i], state)
        results.Insert (0, state)
    results

//
[<CompiledName("ScanBack")>]
let scanBack folder (resizeArray : ResizeArray<'T>) (state : 'State) : ResizeArray<'State> =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    scanBackSub folder resizeArray 0 (length resizeArray - 1) state

//
[<CompiledName("Partition")>]
let partition predicate (resizeArray : ResizeArray<'T>) : ResizeArray<'T> * ResizeArray<'T> =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    let trueResults = ResizeArray ()
    let falseResults = ResizeArray ()

    let len = length resizeArray
    for i = 0 to len - 1 do
        let el = resizeArray.[i]
        if predicate el then
            trueResults.Add el
        else
            falseResults.Add el

    trueResults, falseResults

//
[<CompiledName("MapPartition")>]
let mapPartition partitioner (resizeArray : ResizeArray<'T>) : ResizeArray<'U1> * ResizeArray<'U2> =
    // Preconditions
    checkNonNull "resizeArray" resizeArray

    let results1 = ResizeArray ()
    let results2 = ResizeArray ()

    let len = length resizeArray
    for i = 0 to len - 1 do
        match partitioner resizeArray.[i] with
        | Choice1Of2 value ->
            results1.Add value
        | Choice2Of2 value ->
            results2.Add value

    results1, results2
