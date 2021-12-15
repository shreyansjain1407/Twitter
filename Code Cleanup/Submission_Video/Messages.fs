module Messages

open System
open System.Collections.Generic
open Akka.Actor
open Akka.Configuration
open Akka.FSharp


type NewAnswer =
    {
        Text: string
    }
type Answer = 
    {
        Text: string
        AnswerId: int
    }


type RespMsg =
    {
        Comment: string
        Content: list<string>
        status: int
        error: bool
    }
      

type Register =
    {
        UserName: string
        Password: string
    }

type Login =
    {
        UserName: string
        Password: string
    }

type Logout =
    {
        UserName: string
    }

type Follower = 
    {
        UserName: string
        Following: string
    }

type NewTweet =
    {
        Tweet: string
        UserName: string
    }

type tweetHandlerMsg =
    | AddTweetMsg of NewTweet
    | AddTweetToFollowersMsg of NewTweet
    | TweetParserMsg of NewTweet