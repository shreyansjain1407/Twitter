open Akka.Configuration
open Messages

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
let curClientID = fsi.CommandLineArgs.[3] |> string //This is the ClientID aka, terminal ID
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

//Printer: To print messages / outputs
let Printer(mailbox: Actor<_>) =
    let rec loop() = actor {
        let! (Msg:obj) = mailbox.Receive()
        printfn $"Msg"
        return! loop()
    }
    loop()

let printer = spawn system "Printer" Printer

let User (mailbox:Actor<_>) =
    let mutable curID = ""
    let mutable onlineStatus = false
    let mutable list_Clients = []
    let mutable server = ActorSelection()
    let mutable totalUsers = 0
    //A bunch of random strings that will be implemented as needed
    
    let mutable ClientID = "" //cliID
    let mutable popularHashTags = [] //topHashTags
    let mutable curTweets = 0 //tweetCount
    let mutable interval = 0.0
    
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with
        | Client_Messages.UserReady(userID', list_Clients', server', totalUsers', curClientID', popularHashTags', time) ->
            curID <- userID'
            list_Clients <- list_Clients'
            server <- server'
            totalUsers <- totalUsers'
            ClientID <- curClientID'
            popularHashTags <- popularHashTags'
            interval <- (float) time
            system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(50.), mailbox.Self, Action)
            system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(40.), mailbox.Self, ActionTweet)
        | Client_Messages.Action ->
            if onlineStatus then
                let actions = ["follow";"queryM";"queryHT"]
                match actions.[Random().Next(actions.Length)] with
                | "follow" -> //Follow a user
                    let mutable follUser = (string)[1..totalUsers].[Random().Next(totalUsers)]
                    let mutable client = list_Clients.[Random().Next(list_Clients.Length)]
                    let mutable toBeFollowed = sprintf "%s_%s" client follUser
                    while toBeFollowed = curID do
                        follUser <- (string)[1 .. totalUsers].[Random().Next(totalUsers)]
                        toBeFollowed <- sprintf "%s_%s" client follUser
                    server <! ("Follow", client, curID, toBeFollowed, DateTime.Now)
                | "queryM" ->
                    ()
                | "queryHT" ->
                    ()
                | _ -> ignore()
            
        | _ -> ()
        return! loop()
    }
    loop()