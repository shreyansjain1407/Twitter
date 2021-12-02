#time "on"
#r "nuget: Akka"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#r "nuget: Akka.TestKit"
#load "Messages.fsx"
#load "Functions.fsx"

open System
open Akka.Actor
open Akka.FSharp
open Akka.Configuration
open Messages


let curIP = (string) fsi.CommandLineArgs.[1] //This is the IP of the current client
let curPort = (string) fsi.CommandLineArgs.[2] //This is the port that the client is available on 
let curClientID = (string) fsi.CommandLineArgs.[3] //This is the ClientID aka, terminal ID
let totalUsers = (string) fsi.CommandLineArgs.[4] //This is the total number of users that can be spread across "N" clients 
let totalClients = (string) fsi.CommandLineArgs.[5] //These are the "N" Clients mentioned above 
let mainServerIP = (string) fsi.CommandLineArgs.[6] //This is where we will be accessing out server //Can set to static if needed
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
                hostname = localhost
            }
    }" curPort)
let system = ActorSystem.Create("ClientSideTwitter", configuration)

//Printer: To print messages / outputs
let Printer(mailbox: Actor<_>) =
    let rec loop() = actor {
        let! (msg:obj) = mailbox.Receive()
        printfn $"{msg}"
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
    let mutable ClientID = ""
    let mutable popularHashTags = []
    let mutable curTweets = 0
    let mutable interval = 0.0
    
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with
        | UserReady(userID', list_Clients', server', totalUsers', curClientID', popularHashTags', time) ->
            printfn "Printed at: UserReady"
            curID <- userID'
            list_Clients <- list_Clients'
            server <- server'
            totalUsers <- totalUsers'
            ClientID <- curClientID'
            popularHashTags <- popularHashTags'
            interval <- (float) time
            system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(50.), mailbox.Self, Action)
            system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(40.), mailbox.Self, ClientTweet)
        | Action ->
            printfn "Printed at: Action"
            if onlineStatus then
                let actions = ["follow";"queryM";"queryHT"]
                match actions.[random.Next(actions.Length)] with
                | "follow" -> //Follow a user
                    let mutable follUser = (string)[1..totalUsers].[random.Next(totalUsers)]
                    let mutable client = list_Clients.[random.Next(list_Clients.Length)]
                    let mutable toBeFollowed = sprintf $"{client}_{follUser}"
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
        | ClientTweet ->
            printfn "Printed at: ClientTweet"
            if onlineStatus then
                let tweets = ["tweet";"retweet";"hashtweet";"hashmention"]
                let curTime = DateTime.Now
                match tweets.[random.Next(tweets.Length)] with
                | "tweet" ->
                    curTweets <- curTweets + 1
                    server <! ("Tweet",ClientID, curID, sprintf $"{curID} Tweeted ->tweet_{curTweets}", curTime)
                | "retweet" ->
                    server <! ("ReTweet",ClientID, curID, sprintf $"user {curID} doing re-tweet",curTime)
                | "hashtweet" ->
                    curTweets <- curTweets + 1
                    let ht = popularHashTags.[random.Next(popularHashTags.Length)]
                    server <! ("Tweet",ClientID, curID, sprintf $"{curID} Tweeted -> tweet_{curTweets} with hashtag #{ht}", curTime)
                | "hashmention" ->
                    let mutable mentUser = (string)[1..totalUsers].[random.Next(totalUsers)]
                    let mutable client = list_Clients.[random.Next(list_Clients.Length)]
                    let mutable toBeMentioned = sprintf "%s_%s" client mentUser
                    while mentUser = curID do
                        mentUser <- (string) [1..totalUsers].[random.Next(totalUsers)]
                        toBeMentioned <- sprintf "%s_%s" client mentUser
                    let ht = popularHashTags.[random.Next(popularHashTags.Length)]
                    curTweets <- curTweets + 1
                    let tweet = sprintf $"{curID} tweeted tweet_{curTweets} with hashtag #{ht} and mentioned @{toBeMentioned}"
                    server <! ("Tweet",ClientID, curID, tweet, curTime)
                | _ -> ()
                system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(interval), mailbox.Self, ClientTweet)
        | RequestStatOffline ->
            printfn "Printed at: RequestStatOffline"
            onlineStatus <- false
        | RequestStatOnline ->
            printfn "Printed at: RequestStatOnline"
            onlineStatus <- true
            system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(100.0), mailbox.Self, Action)
            system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(101.0), mailbox.Self, ClientTweet)
        | _ -> ()
        return! loop()
    }
    loop()
    
let UserAdmin (mailbox:Actor<_>) =
    let mutable ClientID = ""
    let mutable totalUsers = 0
    let mutable totalClients = 0
    let mutable curClientPort = ""
    let mutable list_Clients = []
    let mutable offlineUsers = Set.empty
    let mutable registeredUsers = []
    let mutable userLocation = Map.empty
    let mutable intervals = Map.empty
    let mutable list_Users = []
    let mutable subsrank = Map.empty
    let server = system.ActorSelection(sprintf "akka.tcp://ServerSideTwitter@%s:8776/user/Server" mainServerIP)
    let popularHashTags = ["lockdown";"metoo";"covid19";"blacklivesmatter";"crypto";"crowdfunding";"giveaway";"contest";
                        "blackhistorymonth";"womenshistorymonth";"cryptocurrency";"womensday";"happybirthday";
                        "authentication";"USelections";"bidenharris";"internationalwomensday";"influencermarketing";
                        "distributedsystems";"gogators";"blackfriday";"funny";"womeninstem";"iwon";"photography";
                        "mondaymotivation";"ootd";"vegan";"traveltuesday";"tbt"] //Implement reccent hashtags here
//    =======================================================================
    let rec loop() = actor {
        let! (msg:obj) = mailbox.Receive()
        let (messageType,_,_,_,_) : Tuple<string,string,string,string,string> = downcast msg
        match messageType with
        | "Commence" ->
            let (_,id',totalUsers',totalClients',curPort') : Tuple<string,string,string,string,string> = downcast msg
            printfn "Printed at: Commence"
            printfn $"Operations Commence at Client: {id'}"
            ClientID <- id'
            totalUsers <- (int32) totalUsers'
            totalClients <- (int32) totalClients'
            curClientPort <- curPort'
            let mutable users = [|1 .. totalUsers|]
            Functions.shuffle users
            list_Users <- Array.toList users
            for i in [1 .. totalUsers] do
                let key = (string) users.[i-1]
                subsrank <- subsrank |> Map.add (sprintf "%s_%s" ClientID key) ((totalUsers-1)/i)
                intervals <- intervals |> Map.add (sprintf "%s_%s" ClientID key) i
            server <! ("ClientRegister",ClientID, curIP, curPort, DateTime.Now)
            for i in [1..totalUsers] do
                list_Clients <- (string) i :: list_Clients
        | "ClientMessageAck" ->
            printfn "Printed at: ClientMessageAck"
            mailbox.Self <! ("UserRegistration","1","","","")
            system.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(8.0), mailbox.Self, ("SetStatusOffline","","","",""))
        | "UserRegistration" ->
            let (_,a,_,_,_) : Tuple<string,string,string,string,string> = downcast msg 
            printfn "Printed at: UserRegistration"
            let curID' = (int) a
            let mutable curID = sprintf "%s_%s" ClientID ((string) list_Users.[curID' - 1])
            let curLocation = spawn system (sprintf "User_%s" curID) User
            userLocation <- userLocation |> Map.add curID curLocation
            server <! ("UserRegister",ClientID, curID, (string)subsrank.[curID], DateTime.Now)
            registeredUsers <- curID :: registeredUsers
            if curID' < totalUsers then
                system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(40.0), mailbox.Self, ("UserRegistration",string (curID' + 1),"","",""))
        | "UserRegistrationAck" ->
            let (_,incomingID,incomingMsg,_,_) : Tuple<string,string,string,string,string> = downcast msg 
            printfn "Printed at: UserRegistrationAck"
            
            printfn $"{incomingMsg}"
            let temp =
                if totalUsers/100 < 5 then
                    5
                else
                    totalUsers/100
            userLocation.[incomingID] <! UserReady(incomingID, list_Clients, server, totalUsers, ClientID, popularHashTags, temp*intervals.[incomingID])
        | "SetStatusOffline" ->
            printfn "Printed at: SetStatusOffline"
            let mutable total = registeredUsers.Length
            let mutable set = Set.empty
            for i in [1..total] do
                let mutable upcomingOff = registeredUsers.[Random().Next(registeredUsers.Length)]
                while offlineUsers.Contains(upcomingOff) || set.Contains(upcomingOff) do
                    upcomingOff <- registeredUsers.[Random().Next(registeredUsers.Length)]
                server <! ("GoOffline",ClientID, upcomingOff,"",DateTime.Now)
                userLocation.[upcomingOff] <! ("SetStatusOffline","","","","")
                set <- set |> Set.add upcomingOff
            for offlineClient in offlineUsers do
                server <! ("GoOnline",ClientID, offlineClient, "", DateTime.Now)
            offlineUsers <- Set.empty
            offlineUsers <- set
            system.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(8.0), mailbox.Self, ("SetStatusOffline","","","",""))
        | "OnlineAcknowledgement" ->
            let (_,incomingID,_,_,_) : Tuple<string,string,string,string,string> = downcast msg 
            printfn "Printed at: OnlineAcknowledgement"
            userLocation.[incomingID] <! RequestStatOnline
        | _ -> ()
        return! loop()
    }
    loop()

let userAdmin = spawn system "UserAdmin" UserAdmin
printfn $"{curClientID} + {totalUsers} + {totalClients} + {curPort}"
userAdmin <! ("Commence",curClientID, totalUsers, totalClients, curPort)

system.WhenTerminated.Wait()