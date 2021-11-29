#time "on"
#r "nuget: Akka"
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
    | Acknowledgement of string*string //ProcessController as well as Client
    | Initialize of list<string>*int //Initialize needed when following
    //Messages from various users
    | UserReady of (string*list<string>*ActorSelection*int*string*List<string>*int)
    | RequestStatOnline
    | RequestStatOffline
    | Action
    | ClientTweet
    
type ServerToUserAdmin =
    | Commence of string*string*string*string
    | ClientMessageAck
    | UserRegistration of string
    | UserRegistrationAck of string*string
    | SetStatusOffline
    | OnlineAcknowledgement of string
    
//type BossMessages = 
//    | Start of (int*int*int*string)
//    | RegisterUser of (int)
//    | Offline of (string)
//    | Received of (int)
//    | AckUserReg of (string*string)

//type FollowMsg =
//    | Initialize of list<string>*int
    
//type FollowMessages = 
//    | Init of (list<string>*int)

//type UserMessages = 
//    | Ready of (string*list<string>*ActorSelection*int*string*List<string>*int)
//    | GoOnline
//    | GoOffline
//    | Action
//    | ActionTweet

// Currently shall use the Acknowledgement of ProcessController    
//type ClientMessages = 
//    | AckUserReg of (string*string)
//===========Messages Modified as of right now==================


//ServerSideMessages Defined Here
type UserMessages = 
    | Init of IActorRef*IActorRef*IActorRef
    | Register of string*string*DateTime //Shortened to fit only three variables
    | Follow of string*string*string*DateTime
    | Offline of string*string*DateTime
    | Online of string*string*IActorRef*DateTime
    | UpdateUserClientPrinters of Map<string,ActorSelection>
    | UpdateFeeds of string*string*string*DateTime //Shortened to fit only three variables
    | UsersPrint of Map<string,string>*uint64*DateTime

//type TwitterMessages =
//    | InitializeTweet of IActorRef*IActorRef
//    | ServerTweet of string*string*string*DateTime*IActorRef
type UserMessages =
    | RefreshTwitterFeed of (string*string*string*string*DateTime)
    | UpdateFeed of (string*string*string*string*DateTime)

type TweetMessages = 
    | InitializeTweet of (IActorRef*IActorRef) 
    | SendTweet of (string*string*string*DateTime*IActorRef)
    | PrintTweetStats of (Map<string,Set<string>>*Map<string,string>*uint64)
    | AddRetweet of (string)
    | UpdateTwitterInfo of (Map<string,ActorSelection>)

type RetweetMessages = 
    | InitializeRetweet of (IActorRef*IActorRef)
    | Retweet of (string*string*DateTime)
    | RetweetFeed of (string*string*string)
    | UpdateRetweetInfo of (Map<string,ActorSelection>)

type HashTagMessages = 
    | ReadHashTag of (string*string*string)
    | QueryHashtag of (string*string*string*DateTime)
    | UpdateHashTagInfo of (Map<string,ActorSelection>)
    
type MentionsMessages = 
    | InitializeMentions of (IActorRef)
    | MentionsRegister of (string*string)
    | ParseMentions of (string*string*string*DateTime)
    | UpdateMentionsClientPrinters of (Map<string,ActorSelection>)
    | QueryMentions of (string*string*string*DateTime)

type FeedMessages = 
    | ShowFeeds of (string*string*IActorRef)
    | UpdateFeedTable of (string*string*string)
    | UpdateFeedInfo of (Map<string,ActorSelection>)

type ServerMessage = 
    | ReceivedMessage of string*string*string*string*DateTime
