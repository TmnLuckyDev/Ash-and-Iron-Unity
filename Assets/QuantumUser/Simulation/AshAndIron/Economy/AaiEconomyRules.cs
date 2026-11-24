namespace Quantum {
  /// <summary>Shared deterministic spending and population operations.</summary>
  public static class AaiEconomyRules {
    public static bool CanAfford(AaiResources bank, AaiCostSpec cost) {
      return cost.Gold >= 0 && cost.Wood >= 0 && cost.Stone >= 0 &&
        bank.Gold >= cost.Gold && bank.Wood >= cost.Wood && bank.Stone >= cost.Stone;
    }

    public static bool TrySpend(ref AaiPlayer player, AaiCostSpec cost) {
      if (!CanAfford(player.Bank, cost)) return false;
      player.Bank.Gold -= cost.Gold;
      player.Bank.Wood -= cost.Wood;
      player.Bank.Stone -= cost.Stone;
      return true;
    }

    public static bool TryReserveTraining(ref AaiPlayer player, AaiTrainingSpec rule, int count) {
      if (count <= 0 || count > player.PopulationCap || rule.Population <= 0 ||
          rule.Race != player.Race || player.TownHallLevel < rule.RequiredHall) return false;
      long population = (long)rule.Population * count;
      long gold = (long)rule.Cost.Gold * count;
      long wood = (long)rule.Cost.Wood * count;
      long stone = (long)rule.Cost.Stone * count;
      if (population > System.Math.Min(player.PopulationCapacity, player.PopulationCap) - (long)player.PopulationUsed - player.PopulationReserved ||
          gold < 0 || gold > player.Bank.Gold || wood < 0 || wood > player.Bank.Wood || stone < 0 || stone > player.Bank.Stone) return false;
      player.Bank.Gold -= (int)gold;
      player.Bank.Wood -= (int)wood;
      player.Bank.Stone -= (int)stone;
      player.PopulationReserved += (int)population;
      return true;
    }

    public static bool CompleteReservedUnit(ref AaiPlayer player, int population) {
      if (population <= 0 || player.PopulationReserved < population) return false;
      player.PopulationReserved -= population;
      player.PopulationUsed += population;
      return true;
    }
  }
}
