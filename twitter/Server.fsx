open Akka.Configuration
open Messages

#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#r "nuget: Akka.TestKit"
#load "Messages.fsx"
#load "Functions.fsx"

open System
open Akka.Actor
open Akka.FSharp

let curIP = fsi.CommandLineArgs.[1] |> string //This is the IP of the sever

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
                port = 8776
                hostname = %s
            }
    }" curIP)

let system = ActorSystem.Create("ServerSide_Twitter", configuration)

let Tweeter(mailbox:Actor<_>) = 
    let mutable numTweets = 0
    let mutable numUserTweets = Map.empty
    let mutable twitterInfo = Map.empty
    let mutable hashtag = mailbox.Self
    let mutable user = mailbox.Self

    let rec loop() = actor {
        let! msg = mailbox.Receive()

        match msg with
        | InitializeTweet (_user, _hashtag) ->
            user <- _user
            hashtag <- _hashtag
        | SendTweet (clientID, userID, message, time, client) ->
            numTweets <- numTweets + 1

            let mutable currentNumTweet = 0
            if numUserTweets.ContainsKey userID then
                currentNumTweet <- numUserTweets.[userID] + 1
                numUserTweets <- Map.remove userID numUserTweets
            numUserTweets <- Map.add userID currentNumTweet numUserTweets

            twitterInfo.[clientID] <! sprintf "Tweet: %s" message

            hashtag <! ReadHashTag(clientID, userID, message)
            let command = "tweet"
            user <! RefreshTwitterFeed(clientID, userID, message, command, time)
        | AddRetweet userID ->
            let currentNumTweet = numUserTweets.[userID] + 1
            numUserTweets <- Map.remove userID numUserTweets
            numUserTweets <- Map.add userID currentNumTweet numUserTweets
        | UpdateTwitterInfo info ->
            twitterInfo <- info
        return! loop()
    } loop()

let Retweet (mailbox:Actor<_>) = 
    let mutable numRetweets = 0
    let mutable twitterInfo = Map.empty
    let mutable user = mailbox.Self
    let mutable tweeter = mailbox.Self
    let mutable feed = Map.empty

    let rec loop() = actor {
        let! msg = mailbox.Receive()

        match msg with
        | InitializeRetweet (_user, _tweeter) ->
            user <- _user
            tweeter <- _tweeter
        | RetweetFeed (clientID, userID, message) ->
            let mutable tempList = []
            if feed.ContainsKey userID then
                tempList <- feed.[userID]
            tempList <- message :: tempList
            feed <- Map.remove userID feed
            feed <- Map.add userID tempList feed
        | Retweet (clientID, userID, time) ->
            numRetweets <- numRetweets + 1

            let r = Random()
            let randomTweet = feed.[userID].[r.Next(feed.[userID].Length)]
            twitterInfo.[clientID] <! sprintf "%s Retweeted: %s" userID

            let command = "retweet"
            user <! UpdateFeed(clientID, userID, randomTweet, command, DateTime.Now)
            tweeter <! AddRetweet userID
        | UpdateRetweetInfo info ->
            twitterInfo <- info 
        return! loop()
    } loop()

let HashTag (mailbox:Actor<_>) = 
    let mutable twitterInfo = Map.empty
    let mutable hashtags = Map.empty
    let mutable numQuery = 0

    let rec loop() = actor {
        let! msg = mailbox.Receive()

        match msg with
        | ReadHashTag (clientID, userID, message) ->
            let tweet = message.Split ' '
            for t in tweet do
                if t.[0] = '#' then
                        let tag = t.[1 .. (t.Length-1)]
                        if not (hashtags.ContainsKey tag) then
                            hashtags <- Map.add tag List.empty hashtags
                        let mutable tempList = hashtags.[tag]
                        tempList <- message :: tempList
                        hashtags <- Map.remove tag hashtags
                        hashtags <- Map.add tag tempList hashtags
        | QueryHashtag (clientID, userID, hashtag, time) ->
                if twitterInfo.ContainsKey clientID then
                    numQuery <- numQuery + 1
                    if hashtags.ContainsKey hashtag then
                        let mutable hashtagSize = hashtags.[hashtag].Length
                        let mutable tag = ""
                        for i in [0 .. (hashtagSize-1)] do
                            tag <- hashtags.[hashtag].[i] + "\n"
                        twitterInfo.[clientID] <! sprintf "Hastag Query %s by %s" hashtag userID
                    else
                        twitterInfo.[clientID] <! sprintf "Hashtag Query Failed by %s" userID
        | UpdateHashTagInfo info ->
            twitterInfo <- info
        return! loop()
    } loop()

let Mentions (mailbox:Actor<_>) = 
    let mutable twitterInfo = Map.empty
    let mutable mentions = Map.empty
    let mutable numQuery = 0
    let mutable tweeter = mailbox.Self

    let rec loop() = actor {
        let! msg = mailbox.Receive()

        match msg with
        | InitializeMentions _tweeter ->
            tweeter <- _tweeter
        

        return! loop()
    } loop()

let Feed (mailbox:Actor<_>) = 
    let mutable twitterInfo = Map.empty
    let mutable feed = Map.empty

    let rec loop() = actor {
        let! msg = mailbox.Receive()

        match msg with
        | ShowFeeds (clientID, userID, clientAdmin) ->
            if feed.ContainsKey userID then
                let mutable top = ""
                let tempList:List<string> = feed.[userID]
                let mutable feedSize = 5 //arbirtary size
                if tempList.Length < 5 then
                    feedSize <- tempList.Length
                for i in [0 .. (feedSize-1)] do
                    top <- feed.[userID].[i] + "\n"
                twitterInfo.[clientID] <! sprintf "User %s is online. Feed: %s" userID, top
            else
                twitterInfo.[clientID] <! sprintf "User %s is offline" userID
        | UpdateFeedTable (userID, curID, message) ->
            let mutable tempList = []
            if feed.ContainsKey userID then
                tempList <- feed.[userID]
            tempList <- message :: tempList
            feed <- Map.remove userID feed
            feed <- Map.add userID tempList feed
        | UpdateFeedInfo info ->
            twitterInfo <- info
        return! loop()
    } loop()

let UserServer(mailbox:Actor<_>) = 
    let mutable numUsers = Set.empty
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        match msg with
        | Init(reTweeter, feed, tweeter) -> //(IActorRef*IActorRef*IActorRef)
            ()
        | Register(userID, subRank, timeStamp) -> //(string*string*DateTime) //Shortened to fit only three variables
            ()
        | Follow(clientID, userID, followID, timeStamp) -> //(string*string*string*DateTime)
            ()
        | Offline(clientID, userID, timeStamp) -> //(string*string*DateTime)
            ()
        | Online(clientID, userID, userAdmin, timeStamp) -> //(string*string*IActorRef*DateTime)
            ()
        | UpdateUserClientPrinters map -> //(Map<string,ActorSelection>)
            ()
        | UpdateFeeds(userID, incomingTweet, tweetType, timeStamp) -> //(string*string*string*DateTime) //Shortened to fit only three variables
            ()
        | UsersPrint(map, performance, timeStamp) -> //(Map<string,string>*uint64*DateTime)
            ()
        | _ -> ()
        return! loop()
    } loop()

let serverEngine(mailbox:Actor<_>) = 
    let mutable tweeter = mailbox.Self
    let mutable retweeter = mailbox.Self
    let mutable hashtag = mailbox.Self
    let mutable clientInfo = Map.empty
    
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        match msg with
            | ReceivedMessage(msgType,clientID,cur_,cmd,t) ->
                match msgType with
                    | "UserRegister" ->
                        let clientPort = system.ActorSelection(sprintf "akka.tcp://ServerSide_Twitter@%s:%s/user/Printer" cur_ cmd)
                        clientInfo <- Map.add clientID clientPort clientInfo
                        tweeter <! UpdateTwitterInfo(clientInfo)
                        retweeter <! UpdateRetweetInfo(clientInfo)
                        hashtag <! UpdateHashTagInfo(clientInfo)
                    | "GoOnline" ->
                        
                ()
            | _ -> ()
        

        return! loop()         
    } loop()



//let boss = spawn system "Server" ServerActor Line 479