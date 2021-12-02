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
open System.IO
open System.Text
open Messages

let curIP = (string) fsi.CommandLineArgs.[1] //This is the IP of the sever

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

let system = ActorSystem.Create("ServerSideTwitter", configuration)
let path = "stats.txt"
let Tweeter(mailbox:Actor<_>) = 
    let mutable numTweets = 0
    let mutable numUserTweets = Map.empty
    let mutable twitterInfo = Map.empty
    let mutable hashtag = mailbox.Self
    let mutable user = mailbox.Self
    let mutable tweetTime = 0.0
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        let initTime = DateTime.Now
        match msg with
        | InitializeTweet (_user, _hashtag) ->
            printfn "Printed At: InitializeTweet"
            user <- _user
            hashtag <- _hashtag
        | SendTweet (clientID, userID, message, time, client) ->
            printfn "Printed At: SendTweet"
            numTweets <- numTweets + 1

            let mutable currentNumTweet = 0
            if numUserTweets.ContainsKey userID then
                currentNumTweet <- numUserTweets.[userID] + 1
                numUserTweets <- Map.remove userID numUserTweets
            numUserTweets <- Map.add userID currentNumTweet numUserTweets

            twitterInfo.[clientID] <! sprintf "Tweet: %s" message

            hashtag <! ReadHashTag(clientID, userID, message)
            let command = "tweet" 
            user <! UpdateFeeds(userID, message, command, time)

            tweetTime <- tweetTime + (initTime.Subtract time).TotalMilliseconds
            let avgTime = tweetTime / (float) numTweets
            mailbox.Sender() <! ServiceStats("Tweet", avgTime.ToString())
        | AddRetweet userID ->
            printfn "Printed At: AddRetweet"
            let currentNumTweet = numUserTweets.[userID] + 1
            numUserTweets <- Map.remove userID numUserTweets
            numUserTweets <- Map.add userID currentNumTweet numUserTweets
        | UpdateTwitterInfo info ->
            printfn "Printed At: UpdateTwitterInfo"
            twitterInfo <- info
        | PrintTwitterStats (follows, map, performance) ->
            printfn "Printed At: PrintTwitterStats"
            
            File.WriteAllText(path, "")
            File.AppendAllText(path, "\n"+ initTime.ToString())
            File.AppendAllText(path, sprintf "\nNumber User requests per second = %u" performance)
            File.AppendAllText(path, "Average time for service:")
            for s in map do
                File.AppendAllText(path, sprintf "\n%s = %s" s.Key s.Value)
            File.AppendAllText(path, sprintf "\n\nUSerID\tFollowers\tTweets\n")
            for userID in follows do
                if numUserTweets.ContainsKey userID.Key then
                    File.AppendAllText(path, sprintf "%s\t%s\t%s\n" userID.Key (userID.Value.Count |> string) (numUserTweets.[userID.Key] |> string))
        | _ -> ()
        return! loop()
    } 
    loop()

let Retweeter (mailbox:Actor<_>) = 
    let mutable numRetweets = 0
    let mutable twitterInfo = Map.empty
    let mutable user = mailbox.Self
    let mutable tweeter = mailbox.Self
    let mutable feed = Map.empty
    let mutable rtTime = 0.0

    let rec loop() = actor {
        let! msg = mailbox.Receive()
        let initTime = DateTime.Now
        match msg with
        | InitializeRetweet (_user, _tweeter) ->
            printfn "Printed At: InitializeRetweet"
            user <- _user
            tweeter <- _tweeter
        | RetweetFeed (clientID, userID, message) ->
            printfn "Printed At: RetweetFeed"
            let mutable tempList = []
            if feed.ContainsKey userID then
                tempList <- feed.[userID]
            tempList <- message :: tempList
            feed <- Map.remove userID feed
            feed <- Map.add userID tempList feed
        | Retweet (clientID, userID, time) ->
            printfn "Printed At: Retweet"
            numRetweets <- numRetweets + 1

            let r = Random()
            let randomTweet = feed.[userID].[r.Next(feed.[userID].Length)]
            twitterInfo.[clientID] <! sprintf "%s Retweeted: %s" userID

            let command = "retweet" 
            user <! UpdateFeeds(userID, randomTweet, command, DateTime.Now)
            tweeter <! AddRetweet userID

            rtTime <- rtTime + (initTime.Subtract time).TotalMilliseconds
            let avgTime = rtTime / (float) numRetweets
            mailbox.Sender() <! ServiceStats("Retweet", avgTime.ToString())
        | UpdateRetweetInfo info ->
            printfn "Printed At: UpdateRetweetInfo"
            twitterInfo <- info 
        return! loop()
    }
    loop()

let HashTag (mailbox:Actor<_>) = 
    let mutable twitterInfo = Map.empty
    let mutable hashtags = Map.empty
    let mutable numQuery = 1.0
    let mutable totalTime = 1.0

    let rec loop() = actor {
        let! msg = mailbox.Receive()
        let initTime = DateTime.Now
        match msg with
        | ReadHashTag (clientID, userID, message) ->
            printfn "Printed At: ReadHashTag"
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
            printfn "Printed At: QueryHashtag"
            if twitterInfo.ContainsKey clientID then
                numQuery <- numQuery + 1.0
                if hashtags.ContainsKey hashtag then
                    let mutable hashtagSize = hashtags.[hashtag].Length
                    let mutable tag = ""
                    for i in [0 .. (hashtagSize-1)] do
                        tag <- hashtags.[hashtag].[i] + "\n"
                    twitterInfo.[clientID] <! sprintf "Hastag Query %s by %s" hashtag userID
                else
                    twitterInfo.[clientID] <! sprintf "Hashtag Query Failed by %s" userID
                totalTime <- totalTime + (initTime.Subtract time).TotalMilliseconds
                let avgTime = totalTime / numQuery
                mailbox.Sender() <! ServiceStats("QueryHashTag", avgTime.ToString())
        | UpdateHashTagInfo info ->
            printfn "Printed At: UpdateHashTagInfo"
            twitterInfo <- info
        return! loop()
    } 
    loop()

let Mentions (mailbox:Actor<_>) = 
    let mutable twitterInfo = Map.empty
    let mutable users = Set.empty
    let mutable mentions = Map.empty
    let mutable numQuery = 0.0
    let mutable tweeter = mailbox.Self
    let mutable totalTime = 1.0

    let rec loop() = actor {
        let! msg = mailbox.Receive()
        let initTime = DateTime.Now
        match msg with
        | InitializeMentions _tweeter ->
            printfn "Printed At: InitializeMentions"
            tweeter <- _tweeter
        | MentionsRegister(clientID, userID) ->
            printfn "Printed At: MentionsRegister"
            users <- Set.add userID users
            mentions <- Map.add userID List.empty mentions
        | ReadMentions (clientID, userID, tweet, timeStamp) ->
            printfn "Printed At: ReadMentions"
            if users.Contains userID then
                let p = tweet.Split ' '
                let mutable found = false
                for parse in p do
                    if parse.[0] = '@' then
                        found <- true
                        let pm = parse.[1 .. (parse.Length-1)]
                        if users.Contains pm then
                            let mutable tempList = mentions.[pm]
                            tempList <- tweet :: tempList
                            mentions <- Map.remove pm mentions
                            mentions <- Map.add pm tempList mentions
                            tweeter <! SendTweet(clientID, userID, tweet, timeStamp, mailbox.Sender())
                if not found then
                    tweeter <! SendTweet(clientID, userID, tweet, timeStamp, mailbox.Sender())
        | QueryMentions (clientID, userID, mentionedUser, timeStamp) ->
            printfn "Printed At: QueryMentions"
            if twitterInfo.ContainsKey clientID then
                numQuery <- numQuery + 1.0
                if mentions.ContainsKey mentionedUser then
                    let mutable str = ""
                    for i in [0 .. mentions.[mentionedUser].Length] do
                        str <- mentions.[mentionedUser].[i] + "\n"
                totalTime <- totalTime + (initTime.Subtract timeStamp).TotalMilliseconds
                let avg = totalTime / numQuery
                mailbox.Sender() <! ServiceStats("QueryMentions", avg.ToString())
        | UpdateMentionsInfo info ->
            printfn "Printed At: UpdateMentionsInfo"
            twitterInfo <- info
        return! loop()
    } 
    loop()

let Feed (mailbox:Actor<_>) = 
    let mutable twitterInfo = Map.empty
    let mutable feed = Map.empty

    let rec loop() = actor {
        let! msg = mailbox.Receive()

        match msg with
        | ShowFeeds (clientID, userID, clientAdmin) ->
            printfn "Printed At: ShowFeeds"
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
            printfn "Printed At: UpdateFeedTable"
            let mutable tempList = []
            if feed.ContainsKey userID then
                tempList <- feed.[userID]
            tempList <- message :: tempList
            feed <- Map.remove userID feed
            feed <- Map.add userID tempList feed
        | UpdateFeedInfo info ->
            printfn "Printed At: UpdateFeedInfo"
            twitterInfo <- info
        return! loop()
    } 
    loop()

let UserServer(mailbox:Actor<_>) = 
    let mutable tweeter = mailbox.Self
    let mutable retweeter = mailbox.Self
    let mutable feed = mailbox.Self
    let mutable twitterInfo = Map.empty
    let mutable numUsers = Set.empty
    let mutable following = Map.empty
    let mutable subscriber = Map.empty
    let mutable followTime = 1.0
    let mutable userActions = 1
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        let initTime = DateTime.Now
        match msg with
        | Init(reTweeter, _feed, _tweeter) -> //(IActorRef*IActorRef*IActorRef)
            printfn "Printed At: Init"
            retweeter <- reTweeter
            feed <- _feed
            tweeter <- _tweeter 

        | Register(userID, subRank, timeStamp) -> //(string*string*DateTime) //Shortened to fit only three variables
            printfn "Printed At: Register"
            userActions <- userActions + 1
            following <- Map.add userID Set.empty following
            subscriber <- Map.add userID (subRank |> int) subscriber
            followTime <- followTime + (initTime.Subtract timeStamp).TotalMilliseconds
            twitterInfo.[userID] <! sprintf "User %s Registered" userID

        | UserMessages.Follow(clientID, userID, followID, timeStamp) -> //(string*string*string*DateTime)\
            printfn "Printed At: UserMessages"
            userActions <- userActions + 1
            if following.ContainsKey followID && not (following.[followID].Contains userID) && following.[followID].Count < subscriber.[followID] then
                let mutable s = following.[followID]
                s <- Set.add userID s
                following <- Map.remove followID following
                following <- Map.add followID s following
                twitterInfo.[userID] <! sprintf "User %s followed %s" userID followID
            followTime <- followTime + (initTime.Subtract timeStamp).TotalMilliseconds

        | Offline(clientID, userID, timeStamp) -> //(string*string*DateTime)
            printfn "Printed At: Offline"
            userActions <- userActions + 1
            numUsers <- Set.remove userID numUsers
            followTime <- followTime + (initTime.Subtract timeStamp).TotalMilliseconds
            twitterInfo.[userID] <! sprintf "User %s Offline" userID

        | Online(clientID, userID, userAdmin, timeStamp) -> //(string*string*IActorRef*DateTime)
            printfn "Printed At: Online"
            userActions <- userActions + 1
            numUsers <- Set.add userID numUsers
            feed <! ShowFeeds(clientID, userID, userAdmin)
            followTime <- followTime + (initTime.Subtract timeStamp).TotalMilliseconds
            twitterInfo.[userID] <! sprintf "User %s Online" userID

        | UpdateUserInfo info -> //(Map<string,ActorSelection>)
            printfn "Printed At: UpdateUserInfo"
            twitterInfo <- info

        | UpdateFeeds(userID, incomingTweet, tweetType, timeStamp) -> //(string*string*string*DateTime) //Shortened to fit only three variables
            printfn "Printed At: UpdateFeeds"
            userActions <- userActions + 1
            for i in following.[userID] do
                feed <! UpdateFeedTable(userID, tweetType, incomingTweet)
                retweeter <! RetweetFeed(userID, tweetType, incomingTweet)
            followTime <- followTime + (initTime.Subtract timeStamp).TotalMilliseconds

        | UsersPrint(map, performance, timeStamp) -> //(Map<string,string>*uint64*DateTime)
            printfn "Printed At: UsersPrint"
            userActions <- userActions + 1
            tweeter <! PrintTwitterStats(following, map, performance)
            followTime <- followTime + (initTime.Subtract timeStamp).TotalMilliseconds

        | _ -> ()
        
        let avgTime = followTime / (float) userActions
        mailbox.Sender() <! ServiceStats("Follow/Online/Offline", (avgTime.ToString()))
        return! loop()
    } 
    loop()

let serverEngine(mailbox:Actor<_>) = 
    let mutable tweeter = mailbox.Self
    let mutable retweeter = mailbox.Self
    let mutable hashtag = mailbox.Self
    let mutable mentions = mailbox.Self
    let mutable user = mailbox.Self
    let mutable feed = mailbox.Self
    let mutable stats = Map.empty
    let mutable clientInfo = Map.empty
    let mutable clientActions = 0
    let mutable initializedTimeStamp = DateTime.Now
    
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        match msg with
        | Start ->
            printfn "Printed At: Start"
            printfn "Operations commence at server"
            initializedTimeStamp <- DateTime.Now
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
            system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(5000.0), mailbox.Self, PrintStats)
        | ClientRegister(clientID, clientIP, clientPort) ->
            printfn "Printed At: ClientRegister"
            clientActions <- clientActions + 1
            let clientPort = system.ActorSelection(sprintf "akka.tcp://ClientSideTwitter@%s:%s/user/Printer" clientIP clientPort)
            clientInfo <- Map.add clientID clientPort clientInfo
            tweeter <! UpdateTwitterInfo(clientInfo)
            retweeter <! UpdateRetweetInfo(clientInfo)
            hashtag <! UpdateHashTagInfo(clientInfo)
            mentions <! UpdateMentionsInfo(clientInfo)
            feed <! UpdateFeedInfo(clientInfo)
            user <! UpdateUserInfo(clientInfo)
            mailbox.Sender() <! ClientMessageAck
        | UserRegister(clientID, userID, count, timeStamp) ->
            printfn "Printed At: UserRegister"
            clientActions <- clientActions + 1
            user <! Register(clientID, userID, timeStamp)
            mentions <! MentionsRegister(clientID, userID)


        | GoOnline(clientID, userID, timeStamp) ->
            printfn "Printed At: GoOnline"
            clientActions <- clientActions + 1
            user <! Online(clientID, userID, mailbox.Sender(), timeStamp)

        | GoOffline(clientID, userID, timeStamp) ->
            printfn "Printed At: GoOffline"
            clientActions <- clientActions + 1
            user <! Offline(clientID, userID, timeStamp)

        | Follow(clientID, userID, toBeFollowed, timeStamp) ->
            printfn "Printed At: Follow"
            clientActions <- clientActions + 1
            user <! Follow(clientID, userID, toBeFollowed, timeStamp)

        | Tweet(clientID, userID, tweet, timeStamp) ->
            printfn "Printed At: Tweet"
            clientActions <- clientActions + 1
            mentions <! ReadMentions(clientID, userID, tweet, timeStamp)

        | ReTweet(clientID, userId, timeStamp) ->
            printfn "Printed At: ReTweet"
            clientActions <- clientActions + 1
            retweeter <! Retweet(clientID, userId, timeStamp)

        | Mention(clientID, userID, mentionedUser, timeStamp) ->
            printfn "Printed At: Mention"
            clientActions <- clientActions + 1
            mentions <! QueryMentions(clientID, userID, mentionedUser, timeStamp)

        | HashTag(clientID, userID, hashTag, timeStamp) ->
            printfn "Printed At: HashTag"
            clientActions <- clientActions + 1
            hashtag <! QueryHashtag(clientID, userID, hashTag, timeStamp)

        | ServiceStats(key, value) ->
            printfn "Printed At: ServiceStats"
            if key <> "" then
                if stats.ContainsKey key then
                    stats <- Map.remove key stats
            stats <- Map.add key value stats

        | PrintStats ->
            printfn "Printed At: PrintStats"
            let mutable performance = 0
            let timeSpan = (DateTime.Now-initializedTimeStamp).TotalSeconds |> int
            if clientActions > 0 then
                performance <- clientActions/timeSpan
                user <! UsersPrint(stats, performance, DateTime.Now)
                printfn "Server uptime = %u sec, requests = %u, Avg requests = %u per second" timeSpan clientActions performance
            system.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(5000.0), mailbox.Self, PrintStats)

        | _ -> ()
        return! loop()         
    } 
    loop()



let boss = spawn system "Server" serverEngine
boss <! Start
system.WhenTerminated.Wait()