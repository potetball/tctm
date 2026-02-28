using TCTM.Server.DataModel;
using TCTM.Server.Dto;

namespace TCTM.Server.Mappings;

public static class TournamentMappings
{
    public static TournamentDto ToDto(this Tournament tournament)
    {
        return new TournamentDto(
            tournament.Id,
            tournament.Slug,
            tournament.Name,
            tournament.InviteCode,
            tournament.Format,
            tournament.TimeControlPreset,
            tournament.TimeControlMinutes,
            tournament.Status,
            tournament.CreatedAt,
            tournament.Players.Count
        );
    }
}
