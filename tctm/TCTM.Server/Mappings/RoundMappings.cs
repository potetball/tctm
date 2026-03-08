using TCTM.Server.DataModel;
using TCTM.Server.Dto;

namespace TCTM.Server.Mappings;

public static class RoundMappings
{
    public static RoundDto ToDto(this Round round)
    {
        return new RoundDto(
            round.Id,
            round.RoundNumber,
            round.Status,
            round.Matches.Select(m => m.ToDto()).ToList()
        );
    }
}
