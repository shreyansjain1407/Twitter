open System

//Functions being called from actors to randomize the process
let swap (a:_[]) x y =
    let temp = a.[x]
    a.[x] <- a.[y]
    a.[y] <- temp
    
let shuffle a =
    Array.iteri (fun i _ -> swap a i (Random().Next(i,a.Length))) a