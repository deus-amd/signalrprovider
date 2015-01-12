﻿[<ReflectedDefinition>]
module SignalRClient 

open FunScript.TypeScript
open FunScript

open SignalRProvider

let signalR = Globals.Dollar.signalR
let j (s: string) = Globals.Dollar.Invoke(s)
let proxy = signalR.hub.createHubProxy("myHub")
let log = Globals.console.log

let serverHub = new Hubs.myhub(signalR.hub)

let logDeferred s (df: JQueryDeferred<_>) =

    df._doneOverload2 (fun _ -> log <| s + "done") |> ignore
    df.failOverload2 (fun _ -> log <| s + "fail") |> ignore
    ()

let jqIgnore x = 
    x
    null : obj

type ComplexLocalType() =
    member val XX = 42 with get,set
    member val YY = "abc" with get,set

type complexType = SignalRProvider.Types.``SignalRServer!MyServer+ComplexObject``

let onstart () = 
    let compty = new complexType()
    compty.Number <- 43
    compty.Text <- "abc"
    serverHub.functionWith4Args(1, "2", compty, 4) |> ignore
    serverHub.functionWith3Args(1, "2", 3)._doneOverload2( fun (x: obj) -> log <| x.ToString() ) |> ignore
   
    j("#submit").click (fun _ -> 
        serverHub.SendMessage (j("#source")._val() :?> string) |> ignore
        new obj()
        )
    |> ignore
    log "##Started!##"

let printResult (value : string) =
    //sprintf "<p>%s</p>" value
    "<p>"+ value + "" + "</p>"
    |> j("#results").append 
    |> ignore


let main() = 
    Globals.console.log("##Starting:## ")
    signalR.hub.url <- "http://localhost:48213/signalrHub"

    let client = Hubs.myhubClient()
    client.BroadcastMessage <- (fun msg -> printResult msg)
    client.Register(proxy)

    signalR.hub.start onstart

type Wrapper() =
    member this.GenerateScript() = Compiler.compileWithoutReturn <@ main() @>