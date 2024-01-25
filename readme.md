Made by Joonas Pelttari

The current approach does not reset at all but instead finds the target in one run. The next step is to implement a feature to track the squares we have visited and save them to a graph and then when we find the target, reset and do BFS. (if i understood rules correctly).

So the current approach is trying to find the target by checking the possible squares to move and calculating a priority value for every possibility, then choosing the best (lowest) priority. \
The priority is the Euclidian distance from the square to the target + 2.5 * X. \
X = the amount of times we have visited that specific square before.
2.5 is just a number from the sky but it makes the approach to try prioritize unvisited/less visited squares more than the distance, to explore new paths faster.

#### Running the code:
Remember to add an .env file to the project root that has your tokens: \
The app will ask you what level to play, so you don't need every level token. \
PLAYER_TOKEN=XXXXXX \
LEVEL_ID_1=AAAA \
LEVEL_ID_2=BBBB \
LEVEL_ID_3=CCCC \
LEVEL_ID_4=DDDD \
LEVEL_ID_5=EEEE \


#### Project Structure
**Action.cs** implements the moving logic. \
**ClientUtils.cs** implements needed things to communicate with the backend. \
**Program.cs** orchestrates the app.
