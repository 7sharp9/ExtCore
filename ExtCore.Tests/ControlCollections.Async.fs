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

/// Unit tests for the ExtCore.Control.Collections.Async module.
module Tests.ExtCore.Control.Collections.Async

open ExtCore.Control
open ExtCore.Control.Collections
open NUnit.Framework
open FsUnit
//open FsCheck


/// Tests for the ExtCore.Control.Collections.Async.Array module.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Array =
    [<TestCase>]
    let init () : unit =
        Assert.Inconclusive "Test not yet implemented."

    [<TestCase>]
    let map () : unit =
        Assert.Inconclusive "Test not yet implemented."

    [<TestCase>]
    let mapi () : unit =
        Assert.Inconclusive "Test not yet implemented."

    [<TestCase>]
    let map2 () : unit =
        Assert.Inconclusive "Test not yet implemented."

    [<TestCase>]
    let mapPartition () : unit =
        Assert.Inconclusive "Test not yet implemented."

    [<TestCase>]
    let fold () : unit =
        Assert.Inconclusive "Test not yet implemented."

    [<TestCase>]
    let foldi () : unit =
        Assert.Inconclusive "Test not yet implemented."

    [<TestCase>]
    let iter () : unit =
        Assert.Inconclusive "Test not yet implemented."

    [<TestCase>]
    let iteri () : unit =
        Assert.Inconclusive "Test not yet implemented."


/// Tests for the ExtCore.Control.Collections.Async.Seq module.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Seq =

    /// Tests for the ExtCore.Control.Collections.Async.Seq.Parallel module.
    module Parallel =
        [<TestCase>]
        let batch () : unit =
            Assert.Inconclusive "Test not yet implemented."

