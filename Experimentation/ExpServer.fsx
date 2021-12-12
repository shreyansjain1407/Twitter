#r "nuget: Akkling"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"
#load "Message.fsx"

open Message
open System
open Akkling

let configuration = 
    Configuration.parse
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                debug : {
                    receive : on
                    autoreceive : on
                    lifecycle : on
                    event-stream : on
                    unhandled : on
                }
            }
            remote {
                helios.tcp {
                    port = 9002
                    hostname = localhost
                }
            }
        }"

let serversystem = System.create "Server" configuration

let rec server = function
    | Send(num) ->
        let newNumber = num * 10
        printfn "Got a number %d" newNumber
        become server
    | SendStr(str) ->
        printfn $"Received a string {str}"
        become server

let rec serverEngine = function
    | Send(num) ->
        printfn ""
        become serverEngine
    | SendStr(str) ->
        printfn ""
        become serverEngine


let (serverEngine': IActorRef<Message>) = spawn serversystem "server" <| props(actorOf serverEngine)
let (serveRef : IActorRef<Message>) = spawn serversystem "server" <| props(actorOf server)
//let serverRef = serversystem.ActorOf(Props.Create(typeof<server>), "server")
Console.ReadLine() |> ignore
