open Akka.Configuration

#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#r "nuget: Akka.TestKit"
#load "Messages.fsx"

open System
open Akka.Actor
open Akka.FSharp

let curIP = fsi.CommandLineArgs.[1] |> string //This is the IP of the current client
let curPort = fsi.CommandLineArgs.[2] |> string //This is the port that the client is available on 
let curID = fsi.CommandLineArgs.[3] |> string //This is the ClientID aka, terminal ID
let totalUsers = fsi.CommandLineArgs.[4] |> string //This is the total number of users that can be spread across "N" clients 
let totalClients = fsi.CommandLineArgs.[5] |> string //These are the "N" Clients mentioned above 
let mainServerIP = fsi.CommandLineArgs.[6] |> string //This is where we will be accessing out server //Can set to static if needed
let configuration =
    ConfigurationFactory.ParseString(
        sprintf @"akka {            
            stdout-loglevel : DEBUG
            loglevel : ERROR
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            }
            remote.helios.tcp {
                transport-protocol = tcp
                port = %s
                hostname = %s
            }
    }" curPort curIP)
let system = ActorSystem.Create("ClientSide_Twitter", configuration)

