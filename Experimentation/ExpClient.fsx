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
            }
            remote {
                helios.tcp {
                    port = 2552
                    hostname = localhost
                }
            }
        }"

let clientSystem = System.create "client" configuration

let serveRef = select clientSystem "akka.tcp://Server@localhost:9002/user/server"
serveRef <! Message(10)
Console.ReadLine()