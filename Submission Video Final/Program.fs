
open Messages
open System
open System.Collections.Generic
open Akka.Actor
open Akka.FSharp

open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.Writers
open Newtonsoft.Json
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket

let system = ActorSystem.Create("TwitterEngine")

let setCORSHeaders =
    setHeader  "Access-Control-Allow-Origin" "*"
    >=> setHeader "Access-Control-Allow-Headers" "content-type"

let mutable users = Map.empty<string,string>
let mutable CurrentlyActive = Map.empty<string,bool>
let mutable OriginOfTweet = Map.empty
let mutable FollowBindings = Map.empty
let mutable mentions = Map.empty
let mutable hashTags = Map.empty
let mutable webSockets = Map.empty

let buildByteResponseToWS (message:string) =
    message
    |> System.Text.Encoding.ASCII.GetBytes
    |> ByteSegment

let addUser (user: Register) =
    let temp = users.TryFind(user.UserName)
    if temp = None then
        users <- users.Add(user.UserName,user.Password)
        {Comment = "Successfully registered ";Content=[];status=1;error=false}
    else
        {Comment = "The entered user exists!";Content=[];status=1;error=true}

let Login (user: Login) = 
    printfn $"Received request to login from {user.UserName}" 
    let temp = users.TryFind(user.UserName)
    if temp = None then
        {Comment = "User is not registered!";Content=[];status=0;error=true}
    else
        if temp.Value.CompareTo(user.Password) = 0 then
            let temp1 = CurrentlyActive.TryFind(user.UserName)
            if temp1 = None then
                CurrentlyActive <- CurrentlyActive.Add(user.UserName,true)
                {Comment = "You have entered the Twitter Application";Content=[];status=2;error=false}
            else
                {Comment = "User logged in";Content=[];status=2;error=true}
        else
            {Comment = "Password is incorrect!";Content=[];status=1;error=true}

let Logout (user:Logout) = 
    printfn $"Request for logout received from {user.UserName}"
    let temp = users.TryFind(user.UserName)
    if temp = None then
        {Comment = "The user mentioned is not registered";Content=[];status=0;error=true}
    else
        let temp1 = CurrentlyActive.TryFind(user.UserName)
        if temp1 = None then
            {Comment = "User has not logged in!";Content=[];status=1;error=true}
        else
            CurrentlyActive <- CurrentlyActive.Remove(user.UserName)
            {Comment = "Logout successful";Content=[];status=1;error=false}


//Function to check the status of the provided username (Logged In / Logged Out)
let CheckStatus username = 
    let temp = CurrentlyActive.TryFind(username)
    if temp <> None then
        1 
    else
        let temp1 = users.TryFind(username)
        if temp1 = None then
            -1 
        else
            0

//This is a check to verify that the user exists in the registration list
let Exists username =
    let temp = users.TryFind(username)
    temp <> None
    
//Function to handle all of the tasks being completed by the user in real time
let Handler (mailbox:Actor<_>) = 
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with
        |SelfTweet(ws,tweet)->  let response = "You have tweeted '"+tweet.Tweet+"'"
                                let byteResponse = buildByteResponseToWS response
                                let s =socket{
                                                do! ws.send Text byteResponse true
                                                }
                                Async.StartAsTask s |> ignore
        |SendTweet(ws,tweet)->
                                let response = tweet.UserName+" has tweeted '"+tweet.Tweet+"'"
                                let byteResponse = buildByteResponseToWS response
                                let s =socket{
                                                do! ws.send Text byteResponse true
                                                }
                                Async.StartAsTask s |> ignore                    
        |SendMention(ws,tweet)->
                                let response = tweet.UserName+" mentioned you in tweet '"+tweet.Tweet+"'"
                                let byteResponse = buildByteResponseToWS response
                                let s =socket{
                                                do! ws.send Text byteResponse true
                                                }
                                Async.StartAsTask s |> ignore
        |Following(ws,msg)->
                                let response = msg
                                let byteResponse = buildByteResponseToWS response
                                let s =socket{
                                                do! ws.send Text byteResponse true
                                                }
                                Async.StartAsTask s |> ignore
        return! loop()
    }
    loop()
    
let liveUserHandlerRef = spawn system "LiveUser" Handler

let tweetParser (tweet:NewTweet) =
    let splits = (tweet.Tweet.Split ' ')
    for i in splits do
        if i.StartsWith "@" then
            let temp = i.Split '@'
            if Exists temp.[1] then
                let temp1 = mentions.TryFind(temp.[1])
                if temp1 = None then
                    let mutable mp = Map.empty
                    let tempList = List<string>()
                    tempList.Add(tweet.Tweet)
                    mp <- mp.Add(tweet.UserName,tempList)
                    mentions <- mentions.Add(temp.[1],mp)
                else
                    let temp2 = temp1.Value.TryFind(tweet.UserName)
                    if temp2 = None then
                        let tempList = List<string>()
                        tempList.Add(tweet.Tweet)
                        let mutable mp = temp1.Value
                        mp <- mp.Add(tweet.UserName,tempList)
                        mentions <- mentions.Add(temp.[1],mp)
                    else
                        temp2.Value.Add(tweet.Tweet)
                let temp3 = webSockets.TryFind(temp.[1])
                if temp3<>None then
                    liveUserHandlerRef <! SendMention(temp3.Value,tweet)
        elif i.StartsWith "#" then
            let temp1 = i.Split '#'
            let temp = hashTags.TryFind(temp1.[1])
            if temp = None then
                let lst = List<string>()
                lst.Add(tweet.Tweet)
                hashTags <- hashTags.Add(temp1.[1],lst)
            else
                temp.Value.Add(tweet.Tweet)


let addFollower (follower: Follower) =
    printfn $"Request for subscribe received from {follower.UserName} as {follower}"
    let status = CheckStatus follower.UserName
    if status = 1 then
        if (Exists follower.Following) then
            let temp = FollowBindings.TryFind(follower.Following)
            let temp1 = webSockets.TryFind(follower.UserName)
            if temp = None then
                let lst = List<string>()
                lst.Add(follower.UserName)
                FollowBindings <- FollowBindings.Add(follower.Following,lst)
                if temp1 <> None then
                    liveUserHandlerRef <! Following(temp1.Value,"You have successfully been added as a subscriber to "+follower.Following)
                {Comment = "Subscriber list is updated!";Content=[];status=2;error=false}
            else
                if temp.Value.Exists( fun x -> x.CompareTo(follower.UserName) = 0 ) then
                    if temp1 <> None then
                        liveUserHandlerRef <! Following(temp1.Value,"Already subscribed to: "+follower.Following)
                    {Comment = "You already been subscribed to: "+follower.Following;Content=[];status=2;error=true}
                else
                    temp.Value.Add(follower.UserName)
                    if temp1 <> None then
                        liveUserHandlerRef <! Following(temp1.Value,"You are now following: "+follower.Following)
                    {Comment = "Subscriber list is updated!";Content=[];status=2;error=false}
        else
            {Comment = "Subscriber "+follower.Following+" doesn't exist";Content=[];status=2;error=true}
    elif status = 0 then
        {Comment = "Login";Content=[];status=1;error=true}
    else
        {Comment = "Couldn't find User";Content=[];status=0;error=true}

let addTweet (tweet: NewTweet) =
    let temp = OriginOfTweet.TryFind(tweet.UserName)
    if temp = None then
        let lst = List<string>()
        lst.Add(tweet.Tweet)
        OriginOfTweet <- OriginOfTweet.Add(tweet.UserName,lst)
    else
        temp.Value.Add(tweet.Tweet)
    

let addTweetToFollowers (tweet: NewTweet) = 
    let temp = FollowBindings.TryFind(tweet.UserName)
    if temp <> None then
        for i in temp.Value do
            let temp1 = {Tweet=tweet.Tweet;UserName=i}
            addTweet temp1
            let temp2 = webSockets.TryFind(i)
            printfn $"{i}"
            if temp2 <> None then
                liveUserHandlerRef <! SendTweet(temp2.Value,tweet)


let tweetHandler (mailbox:Actor<_>) =
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with 
        | AddTweetMsg(tweet) -> addTweet(tweet)
                                let temp = webSockets.TryFind(tweet.UserName)
                                if temp <> None then
                                    liveUserHandlerRef <! SelfTweet(temp.Value,tweet)
        | AddTweetToFollowersMsg(tweet) ->  addTweetToFollowers(tweet)
        | TweetParserMsg(tweet) -> tweetParser(tweet)
        return! loop()
    }
    loop()

let tweetHandlerRef = spawn system "TweetHandler" tweetHandler

let addTweetToUser (tweet: NewTweet) =
    let status = CheckStatus tweet.UserName
    if status = 1 then
        tweetHandlerRef <! AddTweetMsg(tweet)
        // addTweet tweet
        tweetHandlerRef <! AddTweetToFollowersMsg(tweet)
        // addTweetToFollowers tweet
        tweetHandlerRef <! TweetParserMsg(tweet)
        {Comment = "Sent tweet";Content=[];status=2;error=false}
    elif status = 0 then
        {Comment = "Please Login";Content=[];status=1;error=true}
    else
        {Comment = "Couldn't find User!";Content=[];status=0;error=true}

let getTweets username =
    let status = CheckStatus username
    if status = 1 then
        let temp = OriginOfTweet.TryFind(username)
        if temp = None then
            {Comment = "No Tweet History";Content=[];status=2;error=false}
        else
            let len = Math.Min(10,temp.Value.Count)
            let res = [for i in 1 .. len do yield temp.Value.[i-1]] 
            {Comment = "User tweets task successfully executed!!";Content=res;status=2;error=false}
    elif status = 0 then
        {Comment = "Please login";Content=[];status=1;error=true}
    else
        {Comment = "Couldn't find User";Content=[];status=0;error=true}

let getMentions username = 
    let status = CheckStatus username
    if status = 1 then
        let temp = mentions.TryFind(username)
        if temp = None then
            {Comment = "No Mentions yet";Content=[];status=2;error=false}
        else
            let res = List<string>()
            for i in temp.Value do
                for j in i.Value do
                    res.Add(j)
            let len = Math.Min(10,res.Count)
            let res1 = [for i in 1 .. len do yield res.[i-1]] 
            {Comment = "Get Mentions done successfully";Content=res1;status=2;error=false}
    elif status = 0 then
        {Comment = "Please Login";Content=[];status=1;error=true}
    else
        {Comment = "User Doesn't exist";Content=[];status=0;error=true}

let getHashTags username hashtag =
    let status = CheckStatus username
    if status = 1 then
        printf $"{hashtag}"
        let temp = hashTags.TryFind(hashtag)
        if temp = None then
            {Comment = "No Tweets with this hashtag found";Content=[];status=2;error=false}
        else
            let len = Math.Min(10,temp.Value.Count)
            let res = [for i in 1 .. len do yield temp.Value.[i-1]] 
            {Comment = "Get Hashtags done Successfully";Content=res;status=2;error=false}
    elif status = 0 then
        {Comment = "Please Login";Content=[];status=1;error=true}
    else
        {Comment = "Couldn't find User";Content=[];status=0;error=true}

let registerNewUser (user: Register) =
    printfn $"Request to register from {user.UserName} as {user}"
    addUser user

let respTweet (tweet: NewTweet) =
    printfn $"Sending tweet request received from {tweet.UserName} as {tweet}"
    addTweetToUser tweet

let getString (rawForm: byte[]) =
    System.Text.Encoding.UTF8.GetString(rawForm)

let fromJson<'a> json =
    JsonConvert.DeserializeObject(json, typeof<'a>) :?> 'a


let fetchTweets username =
    printfn $"Received GetTweets Request from {username}"
    getTweets username
    |> JsonConvert.SerializeObject
    |> OK
    >=> setMimeType "application/json"
    >=> setCORSHeaders

let fetchMentions username =
    printfn $"Received GetMentions Request from {username}"
    getMentions username
    |> JsonConvert.SerializeObject
    |> OK
    >=> setMimeType "application/json"
    >=> setCORSHeaders

let fetchHashTags username hashtag =
    printfn $"Received GetHashTag Request from {username} for hashtag {hashtag}"
    getHashTags username hashtag
    |> JsonConvert.SerializeObject
    |> OK
    >=> setMimeType "application/json"
    >=> setCORSHeaders

let register =
    request (fun r ->
    r.rawForm
    |> getString
    |> fromJson<Register>
    |> registerNewUser
    |> JsonConvert.SerializeObject
    |> OK
    )
    >=> setMimeType "application/json"
    >=> setCORSHeaders

let login =
    request (fun r ->
    r.rawForm
    |> getString
    |> fromJson<Login>
    |> Login
    |> JsonConvert.SerializeObject
    |> OK
    )
    >=> setMimeType "application/json"
    >=> setCORSHeaders

let logout =
    request (fun r ->
    r.rawForm
    |> getString
    |> fromJson<Logout>
    |> Logout
    |> JsonConvert.SerializeObject
    |> OK
    )
    >=> setMimeType "application/json"
    >=> setCORSHeaders

let newTweet = 
    request (fun r ->
    r.rawForm
    |> getString
    |> fromJson<NewTweet>
    |> respTweet
    |> JsonConvert.SerializeObject
    |> OK
    )
    >=> setMimeType "application/json"
    >=> setCORSHeaders

let follow =
    request (fun r ->
    r.rawForm
    |> getString
    |> fromJson<Follower>
    |> addFollower
    |> JsonConvert.SerializeObject
    |> OK
    )
    >=> setMimeType "application/json"
    >=> setCORSHeaders




let websocketHandler (webSocket : WebSocket) (context: HttpContext) =
    socket {
        let mutable loop = true

        while loop do
              let! msg = webSocket.read()

              match msg with
              | (Text, data, true) ->
                let str = UTF8.toString data 
                if str.StartsWith("UserName:") then
                    let uname = str.Split(':').[1]
                    webSockets <- webSockets.Add(uname,webSocket)
                    printfn $"connected to {uname} websocket"
                else
                    let response = sprintf $"response to {str}"
                    let byteResponse = buildByteResponseToWS response
                    do! webSocket.send Text byteResponse true

              | (Close, _, _) ->
                let emptyResponse = [||] |> ByteSegment
                do! webSocket.send Close emptyResponse true
                loop <- false
              | _ -> ()
    }


let allow_cors : WebPart =
    choose [
        OPTIONS >=>
            fun context ->
                context |> (
                    setCORSHeaders
                    >=> OK "CORS approved" )
    ]

let app =
    choose
        [ 
            path "/websocket" >=> handShake websocketHandler 
            allow_cors
            GET >=> choose
                [ 
                path "/" >=> OK "The server has booted up successfully. Please register from TwitterClient" 
                pathScan "/gettweets/%s" (fun username -> (fetchTweets username))
                pathScan "/getmentions/%s" (fun username -> (fetchMentions username))
                pathScan "/gethashtags/%s/%s" (fun (username,hashtag) -> (fetchHashTags username hashtag))
                ]

            POST >=> choose
                [   
                path "/newtweet" >=> newTweet 
                path "/register" >=> register
                path "/login" >=> login
                path "/logout" >=> logout
                path "/follow" >=> follow
              ]

            PUT >=> choose
                [ ]

            DELETE >=> choose
                [ ]
        ]

[<EntryPoint>]
let main argv =
    startWebServer defaultConfig app
    0
