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

/// Unit tests for the ExtCore.Control.Collections.Maybe module.
module Tests.ExtCore.Control.Collections.Maybe

open ExtCore.Control
open ExtCore.Control.Collections
open NUnit.Framework
open FsUnit


/// Tests for the ExtCore.Control.Collections.Maybe.Array module.
module Array =
    [<Test>]
    let init () : unit =
        // Test case for an empty array.
        Maybe.Array.init 0 <| fun _ -> None
        |> assertEqual (Some Array.empty)

        // Sample usage test cases.
        Maybe.Array.init 5 <| fun x ->
            Some (x * 7)
        |> assertEqual
            <| Some [| 0; 7; 14; 21; 28; |]

        Maybe.Array.init 5 <| fun x ->
            if x > 0 && x % 5 = 0 then None
            else Some (x * 2)
        |> assertEqual
            <| Some [| 0; 2; 4; 6; 8; |]

        Maybe.Array.init 6 <| fun x ->
            if x > 0 && x % 5 = 0 then None
            else Some (x * 2)
        |> assertEqual None

    [<Test>]
    let iter () : unit =
        // Test case for an empty array.
        do
            let iterationCount = ref 0

            Array.empty
            |> Maybe.Array.iter (fun _ ->
                incr iterationCount
                None)
            |> assertEqual (Some ())

            !iterationCount |> assertEqual 0

        // Sample usage test cases.
        do
            let iterationCount = ref 0

            [| 0..4 |]
            |> Maybe.Array.iter (fun x ->
                incr iterationCount
                if x > 0 && x % 5 = 0 then None
                else Some ())
            |> Option.isSome
            |> should be True

            !iterationCount |> assertEqual 5

        do
            let iterationCount = ref 0

            [| 0..5 |]
            |> Maybe.Array.iter (fun x ->
                incr iterationCount
                if x > 0 && x % 5 = 0 then None
                else Some ())
            |> Option.isNone
            |> should be True

            !iterationCount |> assertEqual 6

        // Test case for short-circuiting.
        do
            let iterationCount = ref 0

            [| 0..5 |]
            |> Maybe.Array.iter (fun x ->
                incr iterationCount
                if x > 0 && x % 2 = 0 then None
                else Some ())
            |> Option.isNone
            |> should be True

            !iterationCount |> assertEqual 3

    [<Test>]
    let iteri () : unit =
        // Test case for an empty array.
        do
            let iterationCount = ref 0

            Array.empty
            |> Maybe.Array.iteri (fun _ _ ->
                incr iterationCount
                None)
            |> assertEqual (Some ())

            !iterationCount |> assertEqual 0

        // Sample usage test cases.
        do
            let iterationCount = ref 0

            [| 0..4 |]
            |> Maybe.Array.iteri (fun idx x ->
                incr iterationCount
                let y = x * idx
                if y > 0 && y % 5 = 0 then None
                else Some ())
            |> Option.isSome
            |> should be True

            !iterationCount |> assertEqual 5

        do
            let iterationCount = ref 0

            [| 0..5 |]
            |> Maybe.Array.iteri (fun idx x ->
                incr iterationCount
                let y = x * idx
                if y > 0 && y % 5 = 0 then None
                else Some ())
            |> Option.isNone
            |> should be True

            !iterationCount |> assertEqual 6

        // Test case for short-circuiting.
        do
            let iterationCount = ref 0

            [| 0..4 |]
            |> Maybe.Array.iteri (fun idx x ->
                incr iterationCount
                let y = x * idx
                if y > 0 && y % 2 = 0 then None
                else Some ())
            |> Option.isNone
            |> should be True

            !iterationCount |> assertEqual 3

    [<Test>]
    let map () : unit =
        // Test case for an empty array.
        Array.empty
        |> Maybe.Array.map (fun _ -> None)
        |> assertEqual (Some Array.empty)

        // Sample usage test cases.
        [| 0..4 |]
        |> Maybe.Array.map (fun x ->
            if x > 0 && x % 5 = 0 then None
            else Some (x * 3))
        |> assertEqual (Some [| 0; 3; 6; 9; 12; |])

        [| 0..5 |]
        |> Maybe.Array.map (fun x ->
            if x > 0 && x % 5 = 0 then None
            else Some (x * 3))
        |> assertEqual None

        // Test case for short-circuiting.
        do
            let iterationCount = ref 0

            [| 0..4 |]
            |> Maybe.Array.map (fun x ->
                incr iterationCount
                if x > 0 && x % 2 = 0 then None
                else Some (x * 3))
            |> assertEqual None

            !iterationCount |> assertEqual 3

    [<Test>]
    let mapi () : unit =
        // Test case for an empty array.
        Array.empty
        |> Maybe.Array.mapi (fun _ _ -> None)
        |> assertEqual (Some Array.empty)

        // Sample usage test cases.
        [| 0..4 |]
        |> Maybe.Array.mapi (fun idx x ->
            let y = x * idx
            if y > 0 && y % 5 = 0 then None
            else Some (y * 3))
        |> assertEqual (Some [| 0; 3; 12; 27; 48; |])

        [| 0..5 |]
        |> Maybe.Array.mapi (fun idx x ->
            let y = x * idx
            if y > 0 && y % 5 = 0 then None
            else Some (y * 3))
        |> assertEqual None

        // Test case for short-circuiting.
        do
            let iterationCount = ref 0

            [| 0..4 |]
            |> Maybe.Array.mapi (fun idx x ->
                incr iterationCount
                let y = x * idx
                if y > 0 && y % 2 = 0 then None
                else Some (y * 3))
            |> assertEqual None

            !iterationCount |> assertEqual 3

    [<Test>]
    let map2 () : unit =
        // Test case for an empty array.
        (Array.empty, Array.empty)
        ||> Maybe.Array.map2 (fun _ _ -> None)
        |> assertEqual (Some Array.empty)

        // Sample usage test cases.
        ([| 0..4 |], [| 1; 1; 2; 3; 5; |])
        ||> Maybe.Array.map2 (fun nat fibo ->
            let x = nat + fibo
            if x >= 10 then None
            else Some x)
        |> assertEqual (Some [| 1; 2; 4; 6; 9; |])

        ([| 0..5 |], [| 1; 1; 2; 3; 5; 8; |])
        ||> Maybe.Array.map2 (fun nat fibo ->
            let x = nat + fibo
            if x >= 10 then None
            else Some x)
        |> assertEqual None

        // Test case for short-circuiting.
        do
            let iterationCount = ref 0

            ([| 0..5 |], [| 1; 1; 2; 3; 5; 8; |])
            ||> Maybe.Array.map2 (fun nat fibo ->
                incr iterationCount
                let x = nat + fibo
                if x >= 2 then None
                else Some x)
            |> assertEqual None

            !iterationCount |> assertEqual 2

    [<Test; ExpectedException(typeof<System.ArgumentException>)>]
    let ``map2 raises exn when arrays have different lengths`` () : unit =
        ([| 0..4 |], [| 0..7|])
        ||> Maybe.Array.map2 (fun _ _ -> None)
        |> ignore

    [<Test>]
    let fold () : unit =
        // Test case for an empty array.
        ("", Array.empty)
        ||> Maybe.Array.fold (fun _ _ -> None)
        |> assertEqual (Some "")

        // Sample usage test cases.
        ("", [| 0..4 |])
        ||> Maybe.Array.fold (fun str x ->
            if x <> 0 && x % 5 = 0 then None
            else Some (str + ((char (int 'a' + x)).ToString ())))
        |> assertEqual (Some "abcde")

        ("", [| 0..5 |])
        ||> Maybe.Array.fold (fun str x ->
            if x <> 0 && x % 5 = 0 then None
            else Some (str + ((char (int 'a' + x)).ToString ())))
        |> assertEqual None

        // Test case for short-circuiting.
        do
            let iterationCount = ref 0

            ("", [| 0..5 |])
            ||> Maybe.Array.fold (fun str x ->
                incr iterationCount
                if x <> 0 && x % 2 = 0 then None
                else Some (str + ((char (int 'a' + x)).ToString ())))
            |> assertEqual None

            !iterationCount |> assertEqual 3

    [<Test>]
    let foldi () : unit =
        // Test case for an empty array.
        ("", Array.empty)
        ||> Maybe.Array.foldi (fun _ _ _ -> None)
        |> assertEqual (Some "")

        // Sample usage test cases.
        ("", [| 0..4 |])
        ||> Maybe.Array.foldi (fun idx str x ->
            let y = x * idx
            if y <> 0 && y % 5 = 0 then None
            else Some (str + ((char (int 'a' + x)).ToString ())))
        |> assertEqual (Some "abcde")

        ("", [| 0..5 |])
        ||> Maybe.Array.foldi (fun idx str x ->
            let y = x * idx
            if y <> 0 && y % 5 = 0 then None
            else Some (str + ((char (int 'a' + x)).ToString ())))
        |> assertEqual None

        // Test case for short-circuiting.
        do
            let iterationCount = ref 0

            ("", [| 0..5 |])
            ||> Maybe.Array.foldi (fun idx str x ->
                incr iterationCount
                let y = x * idx
                if y <> 0 && y % 2 = 0 then None
                else Some (str + ((char (int 'a' + x)).ToString ())))
            |> assertEqual None

            !iterationCount |> assertEqual 3

    [<Test>]
    let reduce () : unit =
        // Sample usage test cases.
        [| 0..4 |]
        |> Maybe.Array.reduce (fun x y ->
            let z = x + y
            if z > 10 then None
            else Some z)
        |> assertEqual (Some 10)

        [| 0..5 |]
        |> Maybe.Array.reduce (fun x y ->
            let z = x + y
            if z > 10 then None
            else Some z)
        |> assertEqual None

        // Test case for short-circuiting.
        do
            let iterationCount = ref 0

            [| 0..5 |]
            |> Maybe.Array.reduce (fun x y ->
                incr iterationCount
                let z = x + y
                if z > 5 then None
                else Some z)
            |> assertEqual None

            !iterationCount |> assertEqual 3

    [<Test; ExpectedException(typeof<System.ArgumentException>)>]
    let ``reduce raises exn for empty array`` () : unit =
        Array.empty
        |> Maybe.Array.reduce (fun _ _ -> None)
        |> ignore


/// Tests for the ExtCore.Control.Collections.Maybe.List module.
module List =
    [<Test>]
    let fold () : unit =
        // Test case for an empty list.
        ("", List.empty)
        ||> Maybe.List.fold (fun _ _ -> None)
        |> assertEqual (Some "")

        // Sample usage test cases.
        ("", [0..4])
        ||> Maybe.List.fold (fun str x ->
            if x <> 0 && x % 5 = 0 then None
            else Some (str + ((char (int 'a' + x)).ToString ())))
        |> assertEqual (Some "abcde")

        ("", [0..5])
        ||> Maybe.List.fold (fun str x ->
            if x <> 0 && x % 5 = 0 then None
            else Some (str + ((char (int 'a' + x)).ToString ())))
        |> assertEqual None

        // Test case for short-circuiting.
        do
            let iterationCount = ref 0

            ("", [0..5])
            ||> Maybe.List.fold (fun str x ->
                incr iterationCount
                if x <> 0 && x % 2 = 0 then None
                else Some (str + ((char (int 'a' + x)).ToString ())))
            |> assertEqual None

            !iterationCount |> assertEqual 3

    [<Test>]
    let iter2 () : unit =
        // Test case for an empty list.
        do
            let iterationCount = ref 0

            (List.empty, List.empty)
            ||> Maybe.List.iter2 (fun _ _ ->
                incr iterationCount
                None)
            |> assertEqual (Some ())

            !iterationCount |> assertEqual 0

        // Sample usage test cases.
        do
            let iterationCount = ref 0

            ([0..4], [1; 1; 2; 3; 5])
            ||> Maybe.List.iter2 (fun nat fibo ->
                incr iterationCount
                let x = nat + fibo
                if x >= 10 then None
                else Some ())
            |> assertEqual (Some ())

            !iterationCount |> assertEqual 5

        do
            let iterationCount = ref 0

            ([0..5], [1; 1; 2; 3; 5; 8])
            ||> Maybe.List.iter2 (fun nat fibo ->
                incr iterationCount
                let x = nat + fibo
                if x >= 10 then None
                else Some ())
            |> assertEqual None

            !iterationCount |> assertEqual 6

        // Test case for short-circuiting.
        do
            let iterationCount = ref 0

            ([0..5], [1; 1; 2; 3; 5; 8])
            ||> Maybe.List.iter2 (fun nat fibo ->
                incr iterationCount
                let x = nat + fibo
                if x >= 2 then None
                else Some ())
            |> assertEqual None

            !iterationCount |> assertEqual 2

    [<Test; ExpectedException(typeof<System.ArgumentException>)>]
    let ``iter2 raises exn when lists have different lengths`` () : unit =
        ([0..4], [0..7])
        ||> Maybe.List.iter2 (fun _ _ -> None)
        |> ignore

    [<Test>]
    let map2 () : unit =
        // Test case for an empty list.
        (List.empty, List.empty)
        ||> Maybe.List.map2 (fun _ _ -> None)
        |> assertEqual (Some List.empty)

        // Sample usage test cases.
        ([0..4], [1; 1; 2; 3; 5])
        ||> Maybe.List.map2 (fun nat fibo ->
            let x = nat + fibo
            if x >= 10 then None
            else Some x)
        |> assertEqual (Some [1; 2; 4; 6; 9])

        ([0..5], [1; 1; 2; 3; 5; 8])
        ||> Maybe.List.map2 (fun nat fibo ->
            let x = nat + fibo
            if x >= 10 then None
            else Some x)
        |> assertEqual None

        // Test case for short-circuiting.
        do
            let iterationCount = ref 0

            ([0..5], [1; 1; 2; 3; 5; 8])
            ||> Maybe.List.map2 (fun nat fibo ->
                incr iterationCount
                let x = nat + fibo
                if x >= 2 then None
                else Some x)
            |> assertEqual None

            !iterationCount |> assertEqual 2

    [<Test; ExpectedException(typeof<System.ArgumentException>)>]
    let ``map2 raises exn when lists have different lengths`` () : unit =
        ([0..4], [0..7])
        ||> Maybe.List.map2 (fun _ _ -> None)
        |> ignore

    [<Test>]
    let mapi2 () : unit =
        // Test case for an empty list.
        (List.empty, List.empty)
        ||> Maybe.List.mapi2 (fun _ _ _ -> None)
        |> assertEqual (Some List.empty)

        // Sample usage test cases.
        ([1..5], [1; 1; 2; 3; 5])
        ||> Maybe.List.mapi2 (fun idx nat fibo ->
            let x = idx + (max nat fibo)
            if x >= 10 then None
            else Some x)
        |> assertEqual (Some [1; 3; 5; 7; 9])

        ([1..6], [1; 1; 2; 3; 5; 8])
        ||> Maybe.List.mapi2 (fun idx nat fibo ->
            let x = idx + (max nat fibo)
            if x >= 10 then None
            else Some x)
        |> assertEqual None

        // Test case for short-circuiting.
        do
            let iterationCount = ref 0

            ([1..5], [1; 1; 2; 3; 5])
            ||> Maybe.List.mapi2 (fun idx nat fibo ->
                incr iterationCount
                let x = idx + (max nat fibo)
                if x >= 2 then None
                else Some x)
            |> assertEqual None

            !iterationCount |> assertEqual 2

    [<Test; ExpectedException(typeof<System.ArgumentException>)>]
    let ``mapi2 raises exn when lists have different lengths`` () : unit =
        ([0..4], [0..7])
        ||> Maybe.List.mapi2 (fun _ _ _ -> None)
        |> ignore


/// Tests for the ExtCore.Control.Collections.Maybe.Seq module.
module Seq =
    [<Test>]
    let iter () : unit =
        // Test case for an empty sequence.
        do
            let iterationCount = ref 0

            Seq.empty
            |> Maybe.Seq.iter (fun _ ->
                incr iterationCount
                None)
            |> assertEqual (Some ())

            !iterationCount |> assertEqual 0

        // Sample usage test cases.
        do
            let iterationCount = ref 0

            [| 0..4 |]
            |> Seq.ofArray
            |> Maybe.Seq.iter (fun x ->
                incr iterationCount
                if x > 0 && x % 5 = 0 then None
                else Some ())
            |> Option.isSome
            |> should be True

            !iterationCount |> assertEqual 5

        do
            let iterationCount = ref 0

            [| 0..5 |]
            |> Seq.ofArray
            |> Maybe.Seq.iter (fun x ->
                incr iterationCount
                if x > 0 && x % 5 = 0 then None
                else Some ())
            |> Option.isNone
            |> should be True

            !iterationCount |> assertEqual 6

        // Test case for short-circuiting.
        do
            let iterationCount = ref 0

            [| 0..5 |]
            |> Seq.ofArray
            |> Maybe.Seq.iter (fun x ->
                incr iterationCount
                if x > 0 && x % 2 = 0 then None
                else Some ())
            |> Option.isNone
            |> should be True

            !iterationCount |> assertEqual 3


/// Tests for the ExtCore.Control.Collections.Maybe.Set module.
module Set =
    [<Test>]
    let fold () : unit =
        // Test case for an empty set.
        ("", Set.empty)
        ||> Maybe.Set.fold (fun _ _ -> None)
        |> assertEqual (Some "")

        // Sample usage test cases.
        ("", set [| 0..4 |])
        ||> Maybe.Set.fold (fun str x ->
            if x <> 0 && x % 5 = 0 then None
            else Some (str + ((char (int 'a' + x)).ToString ())))
        |> assertEqual (Some "abcde")

        ("", set [| 0..5 |])
        ||> Maybe.Set.fold (fun str x ->
            if x <> 0 && x % 5 = 0 then None
            else Some (str + ((char (int 'a' + x)).ToString ())))
        |> assertEqual None

        // Test case for short-circuiting.
        do
            let iterationCount = ref 0

            ("", set [| 0..5 |])
            ||> Maybe.Set.fold (fun str x ->
                incr iterationCount
                if x <> 0 && x % 2 = 0 then None
                else Some (str + ((char (int 'a' + x)).ToString ())))
            |> assertEqual None

            !iterationCount |> assertEqual 3

    [<Test>]
    let mapToArray () : unit =
        // Test case for an empty set.
        Set.empty
        |> Maybe.Set.mapToArray (fun _ -> None)
        |> assertEqual (Some Array.empty)

        // Sample usage test cases.
        set [| 0..4 |]
        |> Maybe.Set.mapToArray (fun x ->
            if x <> 0 && x % 5 = 0 then None
            else Some ((char (int 'a' + x)).ToString ()))
        |> assertEqual (Some [| "a"; "b"; "c"; "d"; "e"; |])

        set [| 0..5 |]
        |> Maybe.Set.mapToArray (fun x ->
            if x <> 0 && x % 5 = 0 then None
            else Some ((char (int 'a' + x)).ToString ()))
        |> assertEqual None

        // Test case for short-circuiting.
        do
            let iterationCount = ref 0

            set [| 0..5 |]
            |> Maybe.Set.mapToArray (fun x ->
                incr iterationCount
                if x <> 0 && x % 2 = 0 then None
                else Some ((char (int 'a' + x)).ToString ()))
            |> assertEqual None

            !iterationCount |> assertEqual 3


/// Tests for the ExtCore.Control.Collections.Maybe.ArrayView module.
module ArrayView =
    [<Test>]
    let fold () : unit =
        // Test case for an empty ArrayView.
        ("", ArrayView.create [| 0..4 |] 0 0)
        ||> Maybe.ArrayView.fold (fun _ _ -> None)
        |> assertEqual (Some "")

        // Sample usage test cases.
        ("", ArrayView.create [| -2..8 |] 2 5)
        ||> Maybe.ArrayView.fold (fun str x ->
            if x <> 0 && x % 5 = 0 then None
            else Some (str + ((char (int 'a' + x)).ToString ())))
        |> assertEqual (Some "abcde")

        ("", ArrayView.create [| -2..8 |] 2 6)
        ||> Maybe.ArrayView.fold (fun str x ->
            if x <> 0 && x % 5 = 0 then None
            else Some (str + ((char (int 'a' + x)).ToString ())))
        |> assertEqual None

        // Test case for short-circuiting.
        do
            let iterationCount = ref 0

            ("", ArrayView.create [| -2..8 |] 2 5)
            ||> Maybe.ArrayView.fold (fun str x ->
                incr iterationCount
                if x <> 0 && x % 2 = 0 then None
                else Some (str + ((char (int 'a' + x)).ToString ())))
            |> assertEqual None

            !iterationCount |> assertEqual 3

