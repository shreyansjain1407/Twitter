#time "on"
#r "nuget: Akka.FSharp"

open System
open Akka.Actor
open Akka.FSharp


type TwitterActions =
    | SendTweet of string
    | Retweet
    | Subscribe

type UserMessage = 
    | Init
    | Tweet of TwitterActions


let system = ActorSystem.Create("System")

let user(mailbox:Actor<_>) =
    let rec loop() = actor {
        let! msg = mailbox.Receive()

        match msg with
        | Tweet action ->
            match action with
            | SendTweet tweet
                
            ()

        return! loop()
    }