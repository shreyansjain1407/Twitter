#time "on"
#r "nuget: Akka"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
#r "nuget: Akka.TestKit"
open System
open Akka.Actor
open Akka.FSharp
open Akka.Configuration

//Client Side Messages Defined Here
type ProcessControllerMsg =
    | Start of int*int*int*string
    | RegisterUser of int
    
type BossMessages = 
    | Start of (int*int*int*string)
    | RegisterUser of (int)
    | Offline of (string)
    | Received of (int)
    | AckUserReg of (string*string)

type FollowMessages = 
    | Init of (list<string>*int)

type UserMessages = 
    | Ready of (string*list<string>*ActorSelection*int*string*List<string>*int)
    | GoOnline
    | GoOffline
    | Action
    | ActionTweet
    
type ClientMessages = 
    | AckUserReg of (string*string)

//ServerSideMessages Defined Here
//type UserMessages = 
//    | Init of (IActorRef*IActorRef*IActorRef)
//    | Register of (string*string*string*DateTime)
//    | Follow of (string*string*string*DateTime)
//    | Offline of (string*string*DateTime)
//    | Online of (string*string*IActorRef*DateTime)
//    | UpdateUserClientPrinters of (Map<string,ActorSelection>)
//    | UpdateFeeds of (string*string*string*string*DateTime)
//    | UsersPrint of (Map<string,string>*uint64*DateTime)

type RetweetMessages = 
    | InitRetweet of (IActorRef*IActorRef)
    | Retweet of (string*string*DateTime)
    | RetweetFeedTable of (string*string*string)
    | UpdateRetweetClientPrinters of (Map<string,ActorSelection>)

type ShowFeedMessages = 
    | ShowFeeds of (string*string*IActorRef)
    | UpdateFeedTable of (string*string*string)
    | UpdateShowFeedClientPrinters of (Map<string,ActorSelection>)

type MentionsMessages = 
    | InitMentions of (IActorRef)
    | MentionsRegister of (string*string)
    | ParseMentions of (string*string*string*DateTime)
    | UpdateMentionsClientPrinters of (Map<string,ActorSelection>)
    | QueryMentions of (string*string*string*DateTime)

type HashTagMessages = 
    | ParseHashTags of (string*string*string)
    | UpdateHashTagsClientPrinters of (Map<string,ActorSelection>)
    | QueryHashtags of (string*string*string*DateTime)

type TweetMessages = 
    | InitTweet of (IActorRef*IActorRef)
    | UpdateTweetsClientPrinters of (Map<string,ActorSelection>)
    | Tweet of (string*string*string*DateTime*IActorRef)
    | PrintTweetStats of (Map<string,Set<string>>*Map<string,string>*uint64)
    | IncTweet of (string)