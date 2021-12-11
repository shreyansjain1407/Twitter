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
    printfn "Got a number %d" num
    become server

let (serveRef : IActorRef<Message>) = spawn serversystem "server" <| props(actorOf server)
//let serverRef = serversystem.ActorOf(Props.Create(typeof<server>), "server")
Console.ReadLine() |> ignore
