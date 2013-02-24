﻿(*

Copyright ____ Chris Okasaki
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

open LanguagePrimitives
open OptimizedClosures
open ExtCore


/// This module creates and manipulates suspensions for lazy evaluation.
module Susp =
    //
    type susp<'a> = Lazy<'a>

    //
    let force (susp : susp<'a>) =
        susp.Force ()

    //
    let delay f : susp<'a> =
        System.Lazy.Create f



/// An implementation of a lazy-list.
type Stream<'T> =
    | Nil
    | Cons of 'T * Stream<'T>
    | LCons of 'T * Lazy<Stream<'T>>

//
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Stream =
    open Susp

    exception Empty

    //
    let empty = Nil

    //
    let cons = Cons

    //
    let lcons (x, xs) =
        LCons (x, delay xs)

    //
    let head = function
        | Nil ->
            raise Empty
        | Cons (x, _) -> x
        | LCons (x, _) -> x

    //
    let tail = function
        | Nil ->
            Nil
        | Cons (_, xs) ->
            xs
        | LCons (_, xs) ->
            force xs

    //
    let isEmpty = function
        | Nil -> true
        | _ -> false

    //
    let rec sizeImpl stream cont =
        match stream with
        | Nil ->
            cont 0
        | Cons (_, xs) ->
            sizeImpl xs <| fun size ->
                1 + size
        | LCons (_, xs) ->
            sizeImpl (force xs) <| fun size ->
                1 + size

    //
    let size stream =
        sizeImpl stream id

