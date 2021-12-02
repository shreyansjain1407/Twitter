
#r "nuget: Akka.FSharp"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#r "nuget: Akka.TestKit"
open System
open Akka.Actor
open Akka.FSharp
//Commence
//Client Side Messages Defined Here
type Client_Messages =
    | Commence of int*int*int*string
    | UserRegistration of int
    | SetOffline of string
    | Receive of int
    | Acknowledgement of string*string 
    | Initialize of list<string>*int 
    //Messages from various users
    | UserReady of (string*list<string>*ActorSelection*int*string*List<string>*int)
    | RequestStatOnline
    | RequestStatOffline
    | Action
    | ClientTweet
    
//type ServerToUserAdmin =
//    | Commence of string*string*string*string
//    | ClientMessageAck
//    | UserRegistration of string
//    | UserRegistrationAck of string*string
//    | SetStatusOffline
//    | OnlineAcknowledgement of string

//ServerSideMessages Defined Here
type UserMessages = 
    | Init of IActorRef*IActorRef*IActorRef
    | Register of string*string*DateTime //Shortened to fit only three variables
    | Follow of string*string*string*DateTime
    | Offline of string*string*DateTime
    | Online of string*string*IActorRef*DateTime
    | UpdateUserInfo of Map<string,ActorSelection>
    | UpdateFeeds of string*string*string*DateTime //Shortened to fit only three variables
    | UsersPrint of Map<string,string>*int*DateTime

//type TwitterMessages =
//    | InitializeTweet of IActorRef*IActorRef
//    | ServerTweet of string*string*string*DateTime*IActorRef
type serverEngineMessages =
    | Start
    | ClientRegister of string*string*string
    | UserRegister of string*string*string*DateTime
    | GoOnline of string*string*DateTime
    | GoOffline of string*string*DateTime
    | Follow of string*string*string*DateTime
    | Tweet of string*string*string*DateTime
    | ReTweet of string*string*DateTime
    | Mention of string*string*string*DateTime
    | HashTag of string*string*string*DateTime
    | ServiceStats of string*string
    | PrintStats

type TweetMessages = 
    | InitializeTweet of IActorRef*IActorRef
    | SendTweet of string*string*string*DateTime*IActorRef
    | PrintTwitterStats of Map<string,Set<string>>*Map<string,string>*int
    | AddRetweet of string
    | UpdateTwitterInfo of Map<string,ActorSelection>

type RetweetMessages = 
    | InitializeRetweet of IActorRef*IActorRef
    | Retweet of string*string*DateTime
    | RetweetFeed of string*string*string
    | UpdateRetweetInfo of Map<string,ActorSelection>

type HashTagMessages = 
    | ReadHashTag of string*string*string
    | QueryHashtag of string*string*string*DateTime
    | UpdateHashTagInfo of Map<string,ActorSelection>
    
type MentionsMessages = 
    | InitializeMentions of IActorRef
    | MentionsRegister of (string*string)
    | ReadMentions of (string*string*string*DateTime)
    | UpdateMentionsInfo of (Map<string,ActorSelection>)
    | QueryMentions of (string*string*string*DateTime)

type FeedMessages = 
    | ShowFeeds of (string*string*IActorRef)
    | UpdateFeedTable of (string*string*string)
    | UpdateFeedInfo of (Map<string,ActorSelection>)

type ServerMessage = 
    | ReceivedMessage of string*string*string*string*DateTime
