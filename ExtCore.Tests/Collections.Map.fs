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

/// Unit tests for the ExtCore.Collections.Map module.
module Tests.ExtCore.Collections.Map

open System
open System.Collections.Generic
open NUnit.Framework
//open FsCheck


[<Test>]
let keys () : unit =
    Map.empty
    |> Map.keys
    |> assertEqual Set.empty
    
    Map.empty
    |> Map.add "Red" ConsoleColor.Red
    |> Map.add "Green" ConsoleColor.Green
    |> Map.add "Blue" ConsoleColor.Blue
    |> Map.add "Black" ConsoleColor.Black
    |> Map.keys
    |> assertEqual (Set.ofArray [| "Red"; "Green"; "Blue"; "Black" |])

[<Test>]
let values () : unit =
    Map.empty
    |> Map.values
    |> assertEqual Set.empty

    Map.empty
    |> Map.add "Red" ConsoleColor.Red
    |> Map.add "Green" ConsoleColor.Green
    |> Map.add "Blue" ConsoleColor.Blue
    |> Map.add "Default" ConsoleColor.Red
    |> Map.values
    |> assertEqual (Set.ofArray [| ConsoleColor.Red; ConsoleColor.Green; ConsoleColor.Blue |])

[<Test>]
let ofKvpSeq () : unit =
    Seq.empty
    |> Map.ofKvpSeq
    |> assertEqual Map.empty

    let expected =
        Map.empty
        |> Map.add "Red" ConsoleColor.Red
        |> Map.add "Green" ConsoleColor.Green
        |> Map.add "Blue" ConsoleColor.Blue
        |> Map.add "Black" ConsoleColor.Black

    seq {
        yield KeyValuePair ("Red", ConsoleColor.Red)
        yield KeyValuePair ("Green", ConsoleColor.Green)
        yield KeyValuePair ("Blue", ConsoleColor.Blue)
        yield KeyValuePair ("Black", ConsoleColor.Black) }
    |> Map.ofKvpSeq
    |> assertEqual expected

[<Test>]
let ofKeys () : unit =
    let expected =
        Map.empty
        |> Map.add ConsoleColor.Red "Red"
        |> Map.add ConsoleColor.Green "Green"
        |> Map.add ConsoleColor.Blue "Blue"
        |> Map.add ConsoleColor.Black "Black"

    Set.ofArray [|
        ConsoleColor.Red; ConsoleColor.Green; ConsoleColor.Blue; ConsoleColor.Black |]
    |> Map.ofKeys (sprintf "%O")
    |> assertEqual expected

[<Test>]
let ofValues () : unit =
    let expected =
        Map.empty
        |> Map.add ConsoleColor.Red "Red"
        |> Map.add ConsoleColor.Green "Green"
        |> Map.add ConsoleColor.Blue "Blue"
        |> Map.add ConsoleColor.Black "Black"

    Set.ofArray [| "Red"; "Green"; "Blue"; "Black" |]
    |> Map.ofValues (fun colorName ->
        Enum.Parse (typeof<System.ConsoleColor>, colorName) :?> System.ConsoleColor)
    |> assertEqual expected

[<Test>]
let removeKeys () : unit =
    let initial =
        Map.empty
        |> Map.add ConsoleColor.Red "Red"
        |> Map.add ConsoleColor.Green "Green"
        |> Map.add ConsoleColor.Blue "Blue"
        |> Map.add ConsoleColor.Black "Black"
        |> Map.add ConsoleColor.Cyan "Cyan"
        |> Map.add ConsoleColor.Gray "Gray"
        |> Map.add ConsoleColor.Yellow "Yellow"
        |> Map.add ConsoleColor.White "White"

    // Removing an empty key set shouldn't modify the map.
    initial
    |> Map.removeKeys Set.empty
    |> assertEqual initial

    let expected =
        Map.empty
        |> Map.add ConsoleColor.Black "Black"
        |> Map.add ConsoleColor.Cyan "Cyan"
        |> Map.add ConsoleColor.Gray "Gray"
        |> Map.add ConsoleColor.Yellow "Yellow"
        |> Map.add ConsoleColor.White "White"

    let rgb =
        Set.ofArray [|
            ConsoleColor.Red; ConsoleColor.Green; ConsoleColor.Blue |]

    // Remove RGB colors from the initial map.
    initial
    |> Map.removeKeys rgb
    |> assertEqual expected

[<Test>]
let filterKeys () : unit =
    let initial =
        Map.empty
        |> Map.add ConsoleColor.Red "Red"
        |> Map.add ConsoleColor.Green "Green"
        |> Map.add ConsoleColor.Blue "Blue"
        |> Map.add ConsoleColor.Black "Black"
        |> Map.add ConsoleColor.Cyan "Cyan"
        |> Map.add ConsoleColor.Gray "Gray"
        |> Map.add ConsoleColor.Yellow "Yellow"
        |> Map.add ConsoleColor.White "White"

    // Filtering with an empty key set should return an empty map.
    initial
    |> Map.filterKeys Set.empty
    |> assertEqual Map.empty

    let expected =
        Map.empty
        |> Map.add ConsoleColor.Red "Red"
        |> Map.add ConsoleColor.Green "Green"
        |> Map.add ConsoleColor.Blue "Blue"

    let rgb =
        Set.ofArray [|
            ConsoleColor.Red; ConsoleColor.Green; ConsoleColor.Blue |]

    // Keep only the RGB colors
    initial
    |> Map.filterKeys rgb
    |> assertEqual expected

[<Test>]
let choose () : unit =
    let initial =
        Map.empty
        |> Map.add ConsoleColor.Red "Red"
        |> Map.add ConsoleColor.Green "Green"
        |> Map.add ConsoleColor.Blue "Blue"
        |> Map.add ConsoleColor.Black "Black"
        |> Map.add ConsoleColor.Cyan "Cyan"
        |> Map.add ConsoleColor.Gray "Gray"
        |> Map.add ConsoleColor.Yellow "Yellow"
        |> Map.add ConsoleColor.White "White"

    let expected =
        Map.empty
        |> Map.add ConsoleColor.Red (255uy, 0uy, 0uy)
        |> Map.add ConsoleColor.Green (0uy, 255uy, 0uy)
        |> Map.add ConsoleColor.Blue (0uy, 0uy, 255uy)

    initial
    |> Map.choose (fun color name ->
        match color, name with
        | ConsoleColor.Red, "Red" ->
            Some (255uy, 0uy, 0uy)
        | ConsoleColor.Green, "Green" ->
            Some (0uy, 255uy, 0uy)
        | ConsoleColor.Blue, "Blue" ->
            Some (0uy, 0uy, 255uy)
        | _ -> None)
    |> assertEqual expected

[<Test>]
let mapPartition () : unit =
    let initial =
        Map.empty
        |> Map.add ConsoleColor.Red "Red"
        |> Map.add ConsoleColor.Green "Green"
        |> Map.add ConsoleColor.Blue "Blue"
        |> Map.add ConsoleColor.Black "Black"
        |> Map.add ConsoleColor.Cyan "Cyan"
        |> Map.add ConsoleColor.Gray "Gray"
        |> Map.add ConsoleColor.Yellow "Yellow"
        |> Map.add ConsoleColor.White "White"

    let left, right =
        initial
        |> Map.mapPartition (fun color colorName ->
            let len = String.length colorName
            if len % 2 = 0 then
                Choice1Of2 color
            else
                Choice2Of2 len)

    left
    |> assertEqual (Map.ofArray
        [| ConsoleColor.Blue, ConsoleColor.Blue;
           ConsoleColor.Cyan, ConsoleColor.Cyan;
           ConsoleColor.Gray, ConsoleColor.Gray;
           ConsoleColor.Yellow, ConsoleColor.Yellow; |])

    right
    |> assertEqual (Map.ofArray
        [| ConsoleColor.Red, 3;
           ConsoleColor.Green, 5;
           ConsoleColor.Black, 5;
           ConsoleColor.White, 5; |])
        
[<Test>]
let union () : unit =
    let initial1 =
        Map.empty
        |> Map.add ConsoleColor.Red "Red"
        |> Map.add ConsoleColor.Green "Green"
        |> Map.add ConsoleColor.Blue "Blue"
        |> Map.add ConsoleColor.Black "Black"
        |> Map.add ConsoleColor.Cyan "Cyan"

    let initial2 =
        Map.empty
        |> Map.add ConsoleColor.Black "Black"
        |> Map.add ConsoleColor.Cyan "Cyna"     // Note the misspelling!
        |> Map.add ConsoleColor.Gray "Gray"
        |> Map.add ConsoleColor.Yellow "Yellow"
        |> Map.add ConsoleColor.White "White"

    let expected =
        Map.empty
        |> Map.add ConsoleColor.Red "Red"
        |> Map.add ConsoleColor.Green "Green"
        |> Map.add ConsoleColor.Blue "Blue"
        |> Map.add ConsoleColor.Black "Black"
        |> Map.add ConsoleColor.Cyan "Cyan"
        |> Map.add ConsoleColor.Gray "Gray"
        |> Map.add ConsoleColor.Yellow "Yellow"
        |> Map.add ConsoleColor.White "White"

    Map.union initial1 initial2
    |> assertEqual expected

[<Test>]
let join () : unit =
    let initial1 =
        Map.empty
        |> Map.add ConsoleColor.Red "Red"
        |> Map.add ConsoleColor.Green "Green"
        |> Map.add ConsoleColor.Blue "Blue"
        |> Map.add ConsoleColor.Black "Black"
        |> Map.add ConsoleColor.Cyan "Cyan"

    let initial2 =
        Map.empty
        |> Map.add ConsoleColor.Black "Black"
        |> Map.add ConsoleColor.Cyan "Cyna"     // Note the misspelling!
        |> Map.add ConsoleColor.Gray "Gray"
        |> Map.add ConsoleColor.Yellow "Yellow"
        |> Map.add ConsoleColor.White "White"

    let expected =
        Map.empty
        |> Map.add ConsoleColor.Red "Red"
        |> Map.add ConsoleColor.Green "Green"
        |> Map.add ConsoleColor.Blue "Blue"
        |> Map.add ConsoleColor.Black "Black"
        |> Map.add ConsoleColor.Cyan "Cyan"
        |> Map.add ConsoleColor.Gray "Gray"
        |> Map.add ConsoleColor.Yellow "Yellow"
        |> Map.add ConsoleColor.White "White"

    (initial1, initial2)
    ||> Map.join (fun _ name1 name2 ->
        if Enum.IsDefined (typeof<ConsoleColor>, name1) then name1
        elif Enum.IsDefined (typeof<ConsoleColor>, name2) then name2
        else "(Unknown)")
    |> assertEqual expected

[<Test>]
let inverse () : unit =
    // Test case for an empty map.
    Map.empty
    |> Map.inverse
    |> Map.isEmpty
    |> assertTrue

    do
        // Sample usage test case.
        let expected =
            Map.empty
            |> Map.add "Black" ConsoleColor.Black
            |> Map.add "Gray" ConsoleColor.Gray
            |> Map.add "Green" ConsoleColor.Green
            |> Map.add "Blue" ConsoleColor.Cyan

        Map.ofArray [|
            ConsoleColor.Black, "Black";
            ConsoleColor.Blue, "Blue";
            ConsoleColor.Cyan, "Blue";
            ConsoleColor.DarkBlue, "Blue";
            ConsoleColor.DarkCyan, "Blue";
            ConsoleColor.DarkGreen, "Green";
            ConsoleColor.Gray, "Gray";
            ConsoleColor.Green, "Green" |]
        |> Map.inverse
        |> assertEqual expected

[<Test>]
let pivot () : unit =
    // Test case for an empty map.
    Map.empty
    |> Map.pivot
    |> Map.isEmpty
    |> assertTrue

    do
        // Sample usage test case.
        let expected =
            Map.empty
            |> Map.add "Black"
                (Set.singleton ConsoleColor.Black)
            |> Map.add "Gray"
                (Set.singleton ConsoleColor.Gray)
            |> Map.add "Green"
                (Set.ofArray [|
                    ConsoleColor.Green; ConsoleColor.DarkGreen; |])
            |> Map.add "Blue"
                (Set.ofArray [|
                    ConsoleColor.Blue; ConsoleColor.Cyan; ConsoleColor.DarkBlue; ConsoleColor.DarkCyan; |])

        Map.ofArray [|
            ConsoleColor.Black, "Black";
            ConsoleColor.Blue, "Blue";
            ConsoleColor.Cyan, "Blue";
            ConsoleColor.DarkBlue, "Blue";
            ConsoleColor.DarkCyan, "Blue";
            ConsoleColor.DarkGreen, "Green";
            ConsoleColor.Gray, "Gray";
            ConsoleColor.Green, "Green" |]
        |> Map.pivot
        |> assertEqual expected
        
[<Test>]
let pivotKeySet () : unit =
    let colorCategory = function
        | ConsoleColor.Black ->
            "Black"
        | ConsoleColor.Blue
        | ConsoleColor.Cyan
        | ConsoleColor.DarkBlue
        | ConsoleColor.DarkCyan ->
            "Blue"
        | ConsoleColor.DarkGray
        | ConsoleColor.Gray ->
            "Gray"
        | ConsoleColor.DarkGreen
        | ConsoleColor.Green ->
            "Green"
        | ConsoleColor.DarkMagenta
        | ConsoleColor.DarkRed
        | ConsoleColor.Magenta
        | ConsoleColor.Red ->
            "Red"
        | ConsoleColor.DarkYellow
        | ConsoleColor.Yellow ->
            "Yellow"
        | ConsoleColor.White ->
            "White"
        | _ ->
            invalidArg "consoleColor" "Invalid System.ConsoleColor value."

    let expected =
        Map.empty
        |> Map.add "Black"
            (Set.singleton ConsoleColor.Black)
        |> Map.add "Gray"
            (Set.singleton ConsoleColor.Gray)
        |> Map.add "Green"
            (Set.ofArray [|
                ConsoleColor.Green; ConsoleColor.DarkGreen; |])
        |> Map.add "Blue"
            (Set.ofArray [|
                ConsoleColor.Blue; ConsoleColor.Cyan; ConsoleColor.DarkBlue; ConsoleColor.DarkCyan; |])

    Set.ofArray [|
        ConsoleColor.Black;
        ConsoleColor.Blue;
        ConsoleColor.Cyan;
        ConsoleColor.DarkBlue;
        ConsoleColor.DarkCyan;
        ConsoleColor.DarkGreen;
        ConsoleColor.Gray;
        ConsoleColor.Green; |]
    |> Map.pivotWith colorCategory
    |> assertEqual expected

[<Test>]
let tryAdd () : unit =
    // Adding to an empty map should always succeed.
    Map.empty
    |> Map.tryAdd ConsoleColor.Blue "Blue"
    |> assertEqual (Map.singleton ConsoleColor.Blue "Blue")

    let initial =
        Map.empty
        |> Map.add ConsoleColor.Red "Red"
        |> Map.add ConsoleColor.Green "Green"
        |> Map.add ConsoleColor.Blue "Blue"
        |> Map.add ConsoleColor.Black "Black"
        |> Map.add ConsoleColor.Cyan "Cyan"

    let expected =
        Map.empty
        |> Map.add ConsoleColor.Red "Red"
        |> Map.add ConsoleColor.Green "Green"
        |> Map.add ConsoleColor.Blue "Blue"
        |> Map.add ConsoleColor.Black "Black"
        |> Map.add ConsoleColor.Cyan "Cyan"
        |> Map.add ConsoleColor.Yellow "Yellow"

    // The binding should be added to the map when there's no existing binding with a given key..
    initial
    |> Map.tryAdd ConsoleColor.Yellow "Yellow"
    |> assertEqual expected

    // The map should be unchanged when a binding already exists with the same key.
    initial
    |> Map.tryAdd ConsoleColor.Blue "DarkBlue"
    |> assertEqual initial

[<Test>]
let tryUpdate () : unit =
    // An empty map can't be updated (the result should always be an empty map).
    Map.empty
    |> Map.tryUpdate ConsoleColor.Blue "Blue"
    |> assertEqual Map.empty

    let initial =
        Map.empty
        |> Map.add ConsoleColor.Red "Red"
        |> Map.add ConsoleColor.Green "Green"
        |> Map.add ConsoleColor.Blue "Blue"
        |> Map.add ConsoleColor.Black "Black"
        |> Map.add ConsoleColor.Cyan "Cyan"

    let expected =
        Map.empty
        |> Map.add ConsoleColor.Red "Red"
        |> Map.add ConsoleColor.Green "Green"
        |> Map.add ConsoleColor.Blue "DarkBlue"
        |> Map.add ConsoleColor.Black "Black"
        |> Map.add ConsoleColor.Cyan "Cyan"

    // The map should be unchanged when there's no existing binding with a given key.
    initial
    |> Map.tryUpdate ConsoleColor.White "White"
    |> assertEqual initial

    // The binding should be updated when it already exists for a given key.
    initial
    |> Map.tryUpdate ConsoleColor.Blue "DarkBlue"
    |> assertEqual expected

[<Test>]
let update () : unit =
    let initial =
        Map.empty
        |> Map.add ConsoleColor.Red "Red"
        |> Map.add ConsoleColor.Green "Green"
        |> Map.add ConsoleColor.Blue "Blue"
        |> Map.add ConsoleColor.Black "Black"
        |> Map.add ConsoleColor.Cyan "Cyan"

    let expected =
        Map.empty
        |> Map.add ConsoleColor.Red "Red"
        |> Map.add ConsoleColor.Green "Green"
        |> Map.add ConsoleColor.Blue "DarkBlue"
        |> Map.add ConsoleColor.Black "Black"
        |> Map.add ConsoleColor.Cyan "Cyan"

    // The binding should be updated when it already exists for a given key.
    initial
    |> Map.update ConsoleColor.Blue "DarkBlue"
    |> assertEqual expected

[<Test; ExpectedException(typeof<KeyNotFoundException>)>]
let ``update raises exn when map doesn't contain key`` () : unit =
    let initial =
        Map.empty
        |> Map.add ConsoleColor.Red "Red"
        |> Map.add ConsoleColor.Green "Green"
        |> Map.add ConsoleColor.Blue "Blue"
        |> Map.add ConsoleColor.Black "Black"
        |> Map.add ConsoleColor.Cyan "Cyan"

    // An exception should be raised when there's no existing binding with a given key.
    initial
    |> Map.update ConsoleColor.White "White"
    |> assertEqual initial

[<Test>]
let countWith () : unit =
    Assert.Ignore "Test not yet implemented."

