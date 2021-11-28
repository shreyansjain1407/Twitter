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
    let random = Random()
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
            system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(40.), mailbox.Self, Tweet)
        | Client_Messages.Action ->
            if onlineStatus then
                let actions = ["follow";"queryM";"queryHT"]
                match actions.[random.Next(actions.Length)] with
                | "follow" -> //Follow a user
                    let mutable follUser = (string)[1..totalUsers].[random.Next(totalUsers)]
                    let mutable client = list_Clients.[random.Next(list_Clients.Length)]
                    let mutable toBeFollowed = sprintf "%s_%s" client follUser
                    while toBeFollowed = curID do
                        follUser <- (string)[1 .. totalUsers].[random.Next(totalUsers)]
                        toBeFollowed <- sprintf "%s_%s" client follUser
                    server <! ("Follow", client, curID, toBeFollowed, DateTime.Now)
                | "queryM" ->
                    let mutable mentUser = (string)[1..totalUsers].[random.Next(totalUsers)]
                    let mutable client = list_Clients.[random.Next(list_Clients.Length)]
                    let mutable toBeMentioned = sprintf "%s_%s" client mentUser
                    server <! ("Mention", client, curID, toBeMentioned, DateTime.Now)
                | "queryHT" ->
                    let ht = popularHashTags.[random.Next(popularHashTags.Length)]
                    server <! ("HashTag",ClientID,curID, ht, DateTime.Now)
                    ()
                | _ -> ()
        | Client_Messages.Tweet ->
            if onlineStatus then
                let tweets = ["tweet";"retweet";"hashtweet";"hashmention"]
                let curTime = DateTime.Now
                match tweets.[random.Next(tweets.Length)] with
                | "tweet" ->
                    curTweets <- curTweets + 1
                    server <! ("Tweet", ClientID, curID, sprintf "Tweet from %s : %d" curID curTweets, curTime)
                | "retweet" ->
                    server <! ("ReTweet", ClientID, curID, sprintf "%s ReTweeted" curID, curTime)
                | "hashtweet" ->
                    curTweets <- curTweets + 1
                    let ht = popularHashTags.[random.Next(popularHashTags.Length)]
                    server <! ("Tweet", ClientID, curID, sprintf "Tweet from %s: %d with #%s" curID curTweets ht, curTime)
                | "hashmention" ->
                    let mutable mentUser = (string)[1..totalUsers].[random.Next(totalUsers)]
                    let mutable client = list_Clients.[random.Next(list_Clients.Length)]
                    let mutable toBeMentioned = sprintf "%s_%s" client mentUser
                    while mentUser = curID do
                        mentUser <- (string) [1..totalUsers].[random.Next(totalUsers)]
                        toBeMentioned <- sprintf "%s_%s" client mentUser
                    let ht = popularHashTags.[random.Next(popularHashTags.Length)]
                    curTweets <- curTweets + 1
                    let tweet = sprintf "Tweet from %s: %d with #%s and @%s" curID curTweets ht toBeMentioned
                    server <! ("Tweet",ClientID, curID, tweet, curTime)
                | _ -> ()
                system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(interval), mailbox.Self, Tweet)
        | Client_Messages.RequestStatOffline ->
            onlineStatus <- false
        | Client_Messages.RequestStatOnline ->
            onlineStatus <- true
            system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(100.0), mailbox.Self, Action)
            system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(101.0), mailbox.Self, Tweet)
        | _ -> ()
        return! loop()
    }
    loop()
    
let UserAdmin (mailbox:Actor<_>) =
    let mutable ClientID = ""
    let mutable totalUsers = 0 //nusers
    let mutable totalClients = 0 //nClients
    let mutable curClientPort = "" //
    let mutable list_Clients = []
    let mutable offlineUsers = Set.empty
    let mutable registeredUsers = []
    let mutable userLocation = Map.empty
    let mutable intervals = Map.empty //intervalMap
    let mutable list_Users = []
    let mutable subsrank = Map.empty
    let server = system.ActorSelection(sprintf "akka.tcp://ServerSide_Twitter@%s:8776/user/Server" mainServerIP)
    let popularHashTags = [] //Implement reccent hashtags here
//    =======================================================================
    let rec loop() = actor {
        let! (msg:obj) = mailbox.Receive()
        let (message,_,_,_,_) : Tuple<string,string,string,string,string> = downcast msg
        match message with
        | "Start" ->
            ()
        | _ -> ()
        return! loop()
    }
    loop()
    
    