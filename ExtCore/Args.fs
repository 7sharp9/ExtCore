﻿(*

Copyright 2005-2009 Microsoft Corporation

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

namespace ExtCore

(* TODO :   Replace the code from the F# PowerPack with a new implementation
            which doesn't require the use of mutable state (e.g., ref cells)
            and which does allow the use of arbitrary parser functions for
            parsing flag values in a type-safe way. For example, this would
            allow DateTime values to be parsed directly from the string then
            returned to the user, instead of parsing the value as a string then
            needing to convert/validate it manually. *)

////
//type ArgInfo<'T> = {
//    //
//    ShortName : char option;
//    //
//    Name : string;
//    //
//    Description : string option;
//    //
//    DefaultValue : 'T option;
//}

(*  The code below is from the F# PowerPack and is only temporary --
    eventually it will be replaced with a new, functional-style API. *)

#nowarn "44"    // Disable warnings from [<Obsolete>]

/// The spec value describes the action of the argument,
/// and whether it expects a following parameter.
type ArgType =
    | ClearArg of bool ref
    | FloatArg of (float -> unit)
    | IntArg of (int -> unit)
    | RestArg of (string -> unit)
    | SetArg of bool ref
    | StringArg of (string -> unit)
    | UnitArg of (unit -> unit)

    static member Clear r = ClearArg r
    static member Float r = FloatArg r
    static member Int r = IntArg r
    static member Rest r = RestArg r
    static member Set r = SetArg r
    static member String r = StringArg r
    static member Unit r = UnitArg r


type ArgInfo (name, action, help) =
    /// Return the name of the argument
    member x.Name = name
    /// Return the argument type and action of the argument
    member x.ArgType = action
    /// Return the usage help associated with the argument
    member x.HelpText = help
  
exception Bad of string
exception HelpText of string

[<Sealed>]
type ArgParser () =
    static let getUsage specs u =
        let sbuf = System.Text.StringBuilder 100  
        let inline pstring (str : string) =
            sbuf.Append str |> ignore
        let inline pendline str =
            pstring str
            pstring "\n"
        pendline u
        specs
        |> List.iter (fun (arg : ArgInfo) ->
            match arg.ArgType with
            | UnitArg _
            | SetArg _
            | ClearArg _ ->
                pstring "\t"; pstring arg.Name; pstring ": "; pendline arg.HelpText
            | StringArg _ ->
                pstring "\t"; pstring arg.Name; pstring " <string>: "; pendline arg.HelpText
            | IntArg _ ->
                pstring "\t"; pstring arg.Name; pstring " <int>: "; pendline arg.HelpText
            | FloatArg _ ->
                pstring "\t"; pstring arg.Name; pstring " <float>: "; pendline arg.HelpText
            | RestArg _ ->
                pstring "\t"; pstring arg.Name; pstring " ...: "; pendline arg.HelpText
                )

        pstring "\t"
        pstring "--help"
        pstring ": "
        pendline "display this list of options"

        pstring "\t"
        pstring "-help"
        pstring ": "
        pendline "display this list of options"

        sbuf.ToString ()

    /// Parse some of the arguments given by 'argv', starting at the given position
    [<System.Obsolete("This method should not be used directly as it will be removed in a future revision of this library")>]
    static member private ParsePartial (cursor, argv, argSpecs : seq<ArgInfo>, ?other, ?usageText) =
        let other = defaultArg other (fun _ -> ())
        let usageText = defaultArg usageText ""
        let nargs = Array.length argv
        incr cursor
        let argSpecs = Seq.toList argSpecs
        let specs =
            argSpecs
            |> List.map (fun (arg : ArgInfo) ->
                arg.Name, arg.ArgType)

        while !cursor < nargs do
            let arg = argv.[!cursor]
            let rec findMatchingArg args =
                match args with
                | [] ->
                    if arg = "-help" || arg = "--help" || arg = "/help" || arg = "/help" || arg = "/?" then
                        raise <| HelpText (getUsage argSpecs usageText)
                    // Note: for '/abc/def' does not count as an argument
                    // Note: '/abc' does
                    elif arg.Length > 0 && (arg.[0] = '-' || (arg.[0] = '/' && not (arg.Length > 1 && arg.[1..].Contains "/"))) then
                        raise <| Bad ("unrecognized argument: "+ arg + "\n" + getUsage argSpecs usageText)
                    else
                       other arg
                       incr cursor

                | ((s, action) :: _) when s = arg ->
                    let getSecondArg () =
                        if !cursor + 1 >= nargs then
                            let msg =
                                sprintf "option %s needs an argument." s
                                + System.Environment.NewLine
                                + getUsage argSpecs usageText
                            raise <| Bad msg
                        argv.[!cursor + 1]
                 
                    match action with
                    | UnitArg f ->
                         f ()
                         incr cursor
                    | SetArg f ->
                         f := true
                         incr cursor
                    | ClearArg f ->
                         f := false
                         incr cursor
                    | StringArg f ->
                         let arg2 = getSecondArg ()
                         f arg2
                         cursor := !cursor + 2
                    | IntArg f ->
                         let arg2 =
                            let arg2 = getSecondArg ()
                            try int32 arg2
                            with _ ->
                                raise <| Bad (getUsage argSpecs usageText)
                         f arg2
                         cursor := !cursor + 2
                    | FloatArg f ->
                         let arg2 =
                            let arg2 = getSecondArg ()
                            try float arg2
                            with _ ->
                                raise <| Bad (getUsage argSpecs usageText)
                         f arg2
                         cursor := !cursor + 2
                    | RestArg f ->
                        incr cursor
                        while !cursor < nargs do
                             f argv.[!cursor]
                             incr cursor

                | (_ :: more) ->
                    findMatchingArg more

            findMatchingArg specs

    /// Prints the help for each argument.
    static member Usage (specs, ?usage) =
        let usage = defaultArg usage ""
        System.Console.Error.WriteLine (getUsage (Seq.toList specs) usage)

    #if FX_NO_COMMAND_LINE_ARGS
    #else
    /// Parse the arguments given by System.Environment.GetEnvironmentVariables()
    /// according to the argument processing specifications "specs".
    /// Args begin with "-". Non-arguments are passed to "f" in
    /// order.  "use" is printed as part of the usage line if an error occurs.
    static member Parse (specs, ?other, ?usageText) =
        let current = ref 0
        let argv = System.Environment.GetCommandLineArgs () 
        try ArgParser.ParsePartial (current, argv, specs, ?other = other, ?usageText = usageText)
        with
            | Bad h
            | HelpText h ->
                System.Console.Error.WriteLine h
                System.Console.Error.Flush ()
                exit 1
            | e ->
                reraise ()
    #endif




