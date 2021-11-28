#time "on"
#r "nuget: Akka.FSharp"
#load "Messages.fsx"


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
//let system = ActorSystem.Create("ServerSide_Twitter", configuration) //Line 35

let user(mailbox:Actor<_>) =
    let rec loop() = actor {
        let! msg = mailbox.Receive()

        match msg with
        | Tweet action ->
            match action with
            | SendTweet tweet      
            | _ -> ()

        return! loop()
    }



//let boss = spawn system "Server" ServerActor Line 479