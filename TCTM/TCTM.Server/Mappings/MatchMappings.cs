using TCTM.Server.DataModel;
using TCTM.Server.Dto;

namespace TCTM.Server.Mappings;

public static class MatchMappings
{
    public static MatchDto ToDto(this Match match)
    {
        return new MatchDto(
            match.Id,
            match.RoundId,
            match.WhitePlayerId,
            match.WhitePlayer?.DisplayName,
            match.BlackPlayerId,
            match.BlackPlayer?.DisplayName,
            match.Result,
            match.Disputed,
            match.Bracket
        );
    }
}
