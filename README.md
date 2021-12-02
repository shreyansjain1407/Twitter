# Twitter
By: Shreyans Jain and Marcus Elosegui

## Running the Program
The two necessary files to run are Server.fsx and Client.fsx which can be found in the 
twitter folder. 

The server should be spun up first. Server.fsx takes in an IP address as an argument. An 
example of running follows as 
    
    dotnet fsi server.fsx localhost

The client takes in an IP address, port, clientID, total users, total clients, and the
server IP. An example of running it would be

    dotnet fsi client.fsx localhost 4521 1 1000 1 localhost
    OR if multiple clients:
    dotnet fsi client.fsx localhost 4521 1 3000 3 localhost
    dotnet fsi client.fsx localhost 4522 2 3000 3 localhost
    dotnet fsi client.fsx localhost 4523 3 3000 3 localhost

## Functionality
The program is able to do all of the following:

- Register accounts
- Send tweets with hashtags and mentions
- Subscribe to other user's tweets
- ReTweet
- Query tweets subscribed to, tweets with specific hashtags, and tweets in which the user
  is mentioned
- Deliver the tweets live if the user is connected

The actors that handle this are Tweeter, Retweeter, HashTag, Mentions, Feed, and UserServer
which can all be found in the Server.fsx file.

## Testing
Testing was done manually by inputting different amounts of users and utilizing multiple
clients. 

The program handled itself well as the number of users increased. The maximum amount of 
simulated users that we tested with was 40000 users. As the number of users increased, so 
did the average time for each server action requested take to complete. Obviously the 
amount of tweets/retweets/hashtags/mentions all went up as well. Below is a table 
showcasing the average amoun of time in milliseconds to complete a request versus the 
number of users with one client running.

| Num Users | Tweets | Retweets | HashTag Query | Mention Query |
| ----------|--------|----------|---------------|---------------|
|    1000   | 15.876 | 14.232   | 13.523        | 13.043        |
| 2000      | 214.581|126.754   | 125.862       | 126.904       |
| 3000      |501.667 |446.197   | 578.365       | 575.290       |
| 4000      |643.472 | 555.845  | 602.045       | 603.586       |
| 5000      | 703.363| 616.489  | 727.902       | 724.738       |

We simulated varying periods of connections and disconnections from the users. We found
that in periods of greater connection the strain on the server was much more noticeable. 
The time taken for each action generally increased. The average amount of server requests
that were successfully handled decreased. During the period of disconnection this trend
reversed in that actions were taken much more swiftly and the server could handle the
number of requests as they decreased.

We simulated a zipf distribution with the amount of subscribers. What we found was a 
progressively significant drop off in the amount of tweets/retweets made by users as 
the number of subscribers decreased. In this model, the vast majority of all tweets made
are made by the most subscribed users, with the amount of tweets practically halving
as the number of subscribers went down.

## Other considerations
The client and engine were in fact separated in different processes split into client.fsx
and server.fsx



