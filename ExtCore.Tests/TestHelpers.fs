﻿(*

Copyright 2005-2009 Microsoft Corporation
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

/// Helper functions for implementing unit tests.
[<AutoOpen>]
module TestHelpers

open System
open System.Collections.Generic
open NUnit.Framework
open FsCheck


/// Asserts that two values are equal.
let assertEqual<'T when 'T : equality> (expected : 'T) (actual : 'T) =
    Assert.AreEqual (expected, actual)




(*  Test helpers from the F# PowerPack.
    TODO : Get rid of most of these -- they can be replaced with FsUnit and built-in NUnit functions. *)

let test msg b = Assert.IsTrue (b, "MiniTest '" + msg + "'")
let logMessage msg = 
    System.Console.WriteLine("LOG:" + msg)
//    System.Diagnostics.Trace.WriteLine("LOG:" + msg)
let check msg v1 v2 = test msg (v1 = v2)
let reportFailure msg = Assert.Fail msg
let numActiveEnumerators = ref 0
let throws f = try f() |> ignore; false with e -> true

let countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd (seq: seq<'a>) =
    let enumerator () = 
        numActiveEnumerators := !numActiveEnumerators + 1;
        let disposed = ref false in
        let endReached = ref false in
        let ie = seq.GetEnumerator() in
        { new System.Collections.Generic.IEnumerator<'a> with 
            member x.Current =
                test "rvlrve0" (not !endReached);
                test "rvlrve1" (not !disposed);
                ie.Current
            member x.Dispose() = 
                test "rvlrve2" !endReached;
                test "rvlrve4" (not !disposed);
                numActiveEnumerators := !numActiveEnumerators - 1;
                disposed := true;
                ie.Dispose() 
        interface System.Collections.IEnumerator with 
            member x.MoveNext() = 
                test "rvlrve0" (not !endReached);
                test "rvlrve3" (not !disposed);
                endReached := not (ie.MoveNext());
                not !endReached
            member x.Current = 
                test "qrvlrve0" (not !endReached);
                test "qrvlrve1" (not !disposed);
                box ie.Current
            member x.Reset() = 
                ie.Reset()
                } in

    { new seq<'a> with 
            member x.GetEnumerator() =  enumerator() 
        interface System.Collections.IEnumerable with 
            member x.GetEnumerator() =  (enumerator() :> _) }

let countEnumeratorsAndCheckedDisposedAtMostOnce (seq: seq<'a>) =
    let enumerator() = 
        let disposed = ref false in
        let endReached = ref false in
        let ie = seq.GetEnumerator() in
        numActiveEnumerators := !numActiveEnumerators + 1;
        { new System.Collections.Generic.IEnumerator<'a> with 
            member x.Current =
                test "qrvlrve0" (not !endReached);
                test "qrvlrve1" (not !disposed);
                ie.Current
            member x.Dispose() = 
                test "qrvlrve4" (not !disposed);
                numActiveEnumerators := !numActiveEnumerators - 1;
                disposed := true;
                ie.Dispose() 
        interface System.Collections.IEnumerator with 
            member x.MoveNext() = 
                test "qrvlrve0" (not !endReached);
                test "qrvlrve3" (not !disposed);
                endReached := not (ie.MoveNext());
                not !endReached
            member x.Current = 
                test "qrvlrve0" (not !endReached);
                test "qrvlrve1" (not !disposed);
                box ie.Current
            member x.Reset() = 
                ie.Reset()
                } in

    { new seq<'a> with 
            member x.GetEnumerator() =  enumerator() 
        interface System.Collections.IEnumerable with 
            member x.GetEnumerator() =  (enumerator() :> _) }

// Verifies two sequences are equal (same length, equiv elements)
let verifySeqsEqual seq1 seq2 =
    if Seq.length seq1 <> Seq.length seq2 then
        // TODO : Provide an error message here.
        Assert.Fail()
        
    let zippedElements = Seq.zip seq1 seq2
    if zippedElements |> Seq.exists (fun (a, b) -> a <> b) then
        // TODO : Provide an error message here.
        Assert.Fail()
        
/// Check that the lamda throws an exception of the given type. Otherwise
/// calls Assert.Fail()
let private checkThrowsExn<'a when 'a :> exn> (f : unit -> unit) : unit =
    let funcThrowsAsExpected =
        try
            do f ()

            // Did not throw -- return an error message explaining this.
            let msg = sprintf "The function did not throw an exception. (Expected: %s)" typeof<'a>.FullName
            Some msg
        with
        | :? 'a ->
            // The expected exception type was raised.
            None
        | ex ->
            // The expected exception type was not raised.
            let msg =
                sprintf "The function raised an exception of type '%s'. (Expected: %s)"
                    (ex.GetType().FullName) typeof<'a>.FullName
            Some msg

    match funcThrowsAsExpected with
    | None -> ()
    | Some msg ->
        Assert.Fail msg

// Illegitimate exceptions. Once we've scrubbed the library, we should add an
// attribute to flag these exception's usage as a bug.
let checkThrowsNullRefException      f = checkThrowsExn<NullReferenceException>   f
let checkThrowsIndexOutRangException f = checkThrowsExn<IndexOutOfRangeException> f

// Legit exceptions
let checkThrowsNotSupportedException f = checkThrowsExn<NotSupportedException>    f
let checkThrowsArgumentException     f = checkThrowsExn<ArgumentException>        f
let checkThrowsArgumentNullException f = checkThrowsExn<ArgumentNullException>    f
let checkThrowsKeyNotFoundException  f = checkThrowsExn<KeyNotFoundException>     f
let checkThrowsDivideByZeroException f = checkThrowsExn<DivideByZeroException>    f
let checkThrowsInvalidOperationExn   f = checkThrowsExn<InvalidOperationException> f

