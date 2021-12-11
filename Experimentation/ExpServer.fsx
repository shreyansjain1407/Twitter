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
| Message(num) ->
    printfn "Got a number %d" num
    become server

let serveRef = spawn serversystem "server" <| props(actorOf server)
Console.ReadLine() |> ignore