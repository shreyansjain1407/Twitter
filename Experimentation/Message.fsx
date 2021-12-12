#r "nuget: Akkling" 
#r "nuget: Akka.Remote"
#r "nuget: Newtonsoft.Json"

type Message = 
| Send of int
| SendStr of string