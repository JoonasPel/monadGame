using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class MovementLogic
{
  private int[] _rotations = new int[] { 0, 45, 90, 135, 180, 225, 270, 315 };
  private Dictionary<(int, int), int> _visitedSquaresCount;
  private static bool _nextActionIsMove = false;

  public MovementLogic()
  {
    _visitedSquaresCount = new Dictionary<(int, int), int>();
  }

  private object MoveAction()
  {
    return new { action = "move" };
  }

  private object RotateAction(int newRotation)
  {
    return new { action = "rotate", rotation = newRotation };
  }

  private double EuclidianDistance(int x1, int x2, int y1, int y2)
  {
    return Math.Sqrt((Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2)));
  }

  public object? GenerateAction(JObject tickData)
  {
    try
    {
      // Check if last round we decided to move next (this) round
      if (_nextActionIsMove == true)
      {
        _nextActionIsMove = false;
        return MoveAction();
      }

      string temp = tickData["gameState"].ToString();
      JObject gameState = JsonConvert.DeserializeObject<JObject>(temp);
      int walls = (int)gameState["square"];
      string wallsBinary = Convert.ToString(walls, 2);
      string wallsPadded = wallsBinary.PadLeft(4, '0');
      int currentRotation = (int)gameState["player"]["rotation"];
      int posX = (int)gameState["player"]["position"]["x"];
      int posY = (int)gameState["player"]["position"]["y"];
      int targetX = (int)gameState["target"]["x"];
      int targetY = (int)gameState["target"]["y"];

      _visitedSquaresCount.TryGetValue((posX, posY), out int count);
      _visitedSquaresCount[(posX, posY)] = count + 1;
      var possibleNewPositions = new PriorityQueue<(int, int), double>();
      // check walls and see where we can go + calculate the priority
      if (wallsPadded[1] == '0') CheckPossiblePos(posX + 1, posY);
      if (wallsPadded[3] == '0') CheckPossiblePos(posX - 1, posY);
      if (wallsPadded[0] == '0') CheckPossiblePos(posX, posY - 1);
      if (wallsPadded[2] == '0') CheckPossiblePos(posX, posY + 1);
      void CheckPossiblePos(int newPosX, int newPosY)
      {
        double dist = EuclidianDistance(newPosX, targetX, newPosY, targetY);
        _visitedSquaresCount.TryGetValue((newPosX, newPosY), out int visitCount);
        // weighting 2.5 is just a number from the sky
        possibleNewPositions.Enqueue((newPosX, newPosY), dist + 2.5 * visitCount);
      }
      // get best square to go
      (int GoToX, int GoToY) = possibleNewPositions.Dequeue();
      // find rotation needed
      int rotationNeeded = -1;
      if (GoToX < posX) rotationNeeded = 270;
      if (GoToX > posX) rotationNeeded = 90;
      if (GoToY < posY) rotationNeeded = 0;
      if (GoToY > posY) rotationNeeded = 180;
      // if we have right rotation atm, move. else rotate now and move next round
      if (currentRotation == rotationNeeded) return MoveAction();
      else
      {
        _nextActionIsMove = true;
        return RotateAction(rotationNeeded);
      }
    }
    catch (Exception e)
    {
      Console.WriteLine(
        $"Encountered Error while Generating Action:\n{e.Message}");
      return null;
    }
  }

  // This can be used to play by yourself
  public object GetManualActionFromUser()
  {
    Console.WriteLine("\nGive action. R45 = rotate 45, M = move");
    do
    {
      string input = Console.ReadLine();
      string regexPattern = @"^R\d{2,3}$";
      if (input == "M") return MoveAction();
      // TODO check that rotation amount is allowed too. e.g. 47 is not, 45 is
      else if (Regex.IsMatch(input, regexPattern))
      {
        int rotation = Convert.ToInt32(input[1..]);
        return RotateAction(rotation);
      }
      else Console.WriteLine("Invalid Input.");
    }
    while (true);
  }
}
