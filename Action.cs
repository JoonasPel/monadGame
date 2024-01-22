using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

public class Action
{
  private readonly Random random;
  private int[] _rotations = new int[] { 0, 45, 90, 135, 180, 225, 270, 315 };

  public Action()
  {
    random = new Random();
  }

  public object GenerateManualActionByUser()
  {
    Console.WriteLine("\nGive action. R45 = rotate 45, M = move");
    do
    {
      string input = Console.ReadLine();
      string regexPattern = @"^R\d{2,3}$";
      if (input == "M")
      {
        return new { action = "move" };
      }
      // TODO check that rotation amount is allowed too. e.g. 47 is not, 45 is
      else if (Regex.IsMatch(input, regexPattern))
      {
        int rotation = Convert.ToInt32(input[1..]);
        return new { action = "rotate", rotation };
      }
      else
      {
        Console.WriteLine("Invalid Input.");
      }
    }
    while (true);
  }
}
