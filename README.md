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
amount of tweets/retweets/hashtags/mentions all went up as well. 

We simulated varying periods of connections and disconnections from the users. We found
that in periods of greater connection the strain on the server was much more noticeable. 
The time taken for each action generally increased. The average amount of server requests
that were successfully handled decreased. During the period of disconnection this trend
reversed in that actions were taken much more swiftly and the server could handle the
number of requests as they decreased.



## Other considerations
The client and engine were in fact separated in different processes split into client.fsx
and server.fsx



