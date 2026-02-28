using TCTM.Server.DataModel;
using TCTM.Server.Dto;

namespace TCTM.Server.Mappings;

public static class StandingMappings
{
    public static StandingDto ToDto(this Standing standing)
    {
        return new StandingDto(
            standing.PlayerId,
            standing.Player.DisplayName,
            standing.Points,
            standing.Wins,
            standing.Draws,
            standing.Losses,
            standing.Buchholz,
            standing.SonnebornBerger
        );
    }
}
