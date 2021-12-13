module Functions

open System
open System.Collections.Generic
open Akka.Actor
open Akka.Configuration
open Akka.FSharp

let buildByteResponseToWS (message:string) =
    message
    |> System.Text.Encoding.ASCII.GetBytes
    |> ByteSegment


let addUser (user: Register) =
    let temp = users.TryFind(user.UserName)
    if temp = None then
        users <- users.Add(user.UserName,user.Password)
        {Comment = "Sucessfully registered ";Content=[];status=1;error=false}
    else
        {Comment = "The entered user exists!";Content=[];status=1;error=true}

let loginuser (user: Login) = 
    printfn "Received Request to login from %s" user.UserName 
    let temp = users.TryFind(user.UserName)
    if temp = None then
        {Comment = "User is not registered!";Content=[];status=0;error=true}
    else
        if temp.Value.CompareTo(user.Password) = 0 then
            let temp1 = activeUsers.TryFind(user.UserName)
            if temp1 = None then
                activeUsers <- activeUsers.Add(user.UserName,true)
                {Comment = "You have entered the Twitter Application";Content=[];status=2;error=false}
            else
                {Comment = "User logged in";Content=[];status=2;error=true}
        else
            {Comment = "Password is incorrect!";Content=[];status=1;error=true}

let logoutuser (user:Logout) = 
    printfn "Request for logout received from %s" user.UserName
    let temp = users.TryFind(user.UserName)
    if temp = None then
        {Comment = "The user mentioned is not registered";Content=[];status=0;error=true}
    else
        let temp1 = activeUsers.TryFind(user.UserName)
        if temp1 = None then
            {Comment = "User has not logged in!";Content=[];status=1;error=true}
        else
            activeUsers <- activeUsers.Remove(user.UserName)
            {Comment = "Logout successful";Content=[];status=1;error=false}

let isUserLoggedIn username = 
    let temp = activeUsers.TryFind(username)
    if temp <> None then
        1 
    else
        let temp1 = users.TryFind(username)
        if temp1 = None then
            -1 
        else
            0

let checkUserExsistance username =
    let temp = users.TryFind(username)
    temp <> None
