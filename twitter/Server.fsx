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

let Retweeter (mailbox:Actor<_>) = 
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
            user <! UpdateFeeds(clientID, userID, randomTweet, command, DateTime.Now)
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
    let mutable tweeter = mailbox.Self
    let mutable retweeter = mailbox.Self
    let mutable feed = mailbox.Self
    let mutable twitterInfo = Map.empty
    let mutable numUsers = Set.empty
    let mutable following = Map.empty
    let mutable subscriber = Map.empty
    let mutable followTime = 1.0
    let mutable userActions = 0
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        match msg with
        | Init(reTweeter, _feed, _tweeter) -> //(IActorRef*IActorRef*IActorRef)
            retweeter <- reTweeter
            feed <- _feed
            tweeter <- _tweeter 
            ()
        | Register(userID, subRank, timeStamp) -> //(string*string*DateTime) //Shortened to fit only three variables
            userActions <- userActions + 1
            following <- Map.add userID Set.empty following
            subscriber <- Map.add userID (subRank |> int) subscriber
            followTime <- followTime + (timeStamp.Subtract timeStamp).TotalMilliseconds
            twitterInfo.[userID] <! sprintf "User %s Registered" userID
            ()
        | UserMessages.Follow(clientID, userID, followID, timeStamp) -> //(string*string*string*DateTime)\
            userActions <- userActions + 1
            if following.ContainsKey followID && not (following.[followID].Contains userID) && following.[followID].Count < subscriber.[followID] then
                let mutable s = following.[followID]
                s <- Set.add userID s
                following <- Map.remove followID following
                following <- Map.add followID s following
                twitterInfo.[userID] <! sprintf "User %s followed %s" userID followID
            followTime <- followTime + (timeStamp.Subtract timeStamp).TotalMilliseconds
            ()
        | Offline(clientID, userID, timeStamp) -> //(string*string*DateTime)
            userActions <- userActions + 1
            numUsers <- Set.remove userID numUsers
            followTime <- followTime + (timeStamp.Subtract timeStamp).TotalMilliseconds
            twitterInfo.[userID] <! sprintf "User %s Offline" userID
            ()
        | Online(clientID, userID, userAdmin, timeStamp) -> //(string*string*IActorRef*DateTime)
            userActions <- userActions + 1
            numUsers <- Set.add userID numUsers
            feed <! ShowFeeds(clientID, userID, userAdmin)
            followTime <- followTime + (timeStamp.Subtract timeStamp).TotalMilliseconds
            twitterInfo.[userID] <! sprintf "User %s Online" userID
            ()
        | UpdateUserInfo info -> //(Map<string,ActorSelection>)
            twitterInfo <- info
            ()
        | UpdateFeeds(userID, incomingTweet, tweetType, timeStamp) -> //(string*string*string*DateTime) //Shortened to fit only three variables
            userActions <- userActions + 1
            for i in following.[userID] do
                feed <! UpdateFeedTable(userID, tweetType, incomingTweet)
                retweeter <! RetweetFeed(userID, tweetType, incomingTweet)
            followTime <- followTime + (timeStamp.Subtract timeStamp).TotalMilliseconds
            ()
        | UsersPrint(map, performance, timeStamp) -> //(Map<string,string>*uint64*DateTime)
            userActions <- userActions + 1
            tweeter <! PrintTweetStats(following, map, performance)
            followTime <- followTime + (timeStamp.Subtract timeStamp).TotalMilliseconds
            ()
        | _ -> ()
        return! loop()
    } loop()

let serverEngine(mailbox:Actor<_>) = 
    let mutable tweeter = mailbox.Self
    let mutable retweeter = mailbox.Self
    let mutable hashtag = mailbox.Self
    let mutable mentions = mailbox.Self
    let mutable user = mailbox.Self
    let mutable feed = mailbox.Self
    let mutable clientInfo = Map.empty
    let mutable clientActions = 0
    
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        match msg with
        | Start ->
            tweeter <- spawn system (sprintf "Tweeter") Tweeter
            retweeter <- spawn system (sprintf "Retweeter") Retweeter
            user <- spawn system (sprintf "User") UserServer
            hashtag <- spawn system (sprintf "HashTag") HashTag
            mentions <- spawn system (sprintf "Mention") Mentions
            feed <- spawn system (sprintf "Feed") Feed

            tweeter <! InitializeTweet(user, hashtag)
            retweeter <! InitializeRetweet(user, tweeter)
            user <! Init(retweeter, feed, tweeter)
            mentions <! InitializeMentions(tweeter)
            system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(5000.0), mailbox.Self, ("PrintStats","","","",DateTime.Now))
            ()
        | ClientRegister(clientID, clientIP, clientPort) -> 
            clientActions <- clientActions + 1
            let clientPort = system.ActorSelection(sprintf "akka.tcp://ServerSide_Twitter@%s:%s/user/Printer" cur_ cmd)
            clientInfo <- Map.add clientID clientPort clientInfo
            tweeter <! UpdateTwitterInfo(clientInfo)
            retweeter <! UpdateRetweetInfo(clientInfo)
            hashtag <! UpdateHashTagInfo(clientInfo)
            ()
        | UserRegister(clientID, userID, count, timeStamp) ->
            clientActions <- clientActions + 1
            user <! Register(clientID, userID, timeStamp)
            mentions <! MentionsRegister(clientID, userID)

            ()
        | GoOnline(clientID, userID, timeStamp) ->
            clientActions <- clientActions + 1
            user <! Online(clientID, userID, mailbox.Sender(), timeStamp)
            ()
        | GoOffline(clientID, userID, timeStamp) ->
            clientActions <- clientActions + 1
            user <! Offline(clientID, userID, timeStamp)
            ()
        | Follow(clientID, userID, toBeFollowed, timeStamp) ->
            clientActions <- clientActions + 1
            user <! Follow(clientID, userID, toBeFollowed, timeStamp)
            ()
        | Tweet(clientID, userID, tweet, timeStamp) ->
            clientActions <- clientActions + 1
            mentions <! ParseMentions(clientID, userID, tweet, timeStamp)
            ()
        | ReTweet(clientID, userId, timeStamp) ->
            clientActions <- clientActions + 1
            retweeter <! Retweet(clientID, userId, timeStamp)
            ()
        | Mention(clientID, userID, mentionedUser, timeStamp) ->
            clientActions <- clientActions + 1
            mentions <! QueryMentions(clientID, userID, mentionedUser, timeStamp)
            ()
        | HashTag(clientID, userID, hashTag, timeStamp) ->
            clientActions <- clientActions + 1
            hashtag <! QueryHashtag(clientID, userID, hashTag, timeStamp)
            ()
        | ServiceStats(key, value) ->
            ()
        | PrintStats ->
            ()
        | _ -> ()
        return! loop()         
    } loop()



//let boss = spawn system "Server" ServerActor Line 479