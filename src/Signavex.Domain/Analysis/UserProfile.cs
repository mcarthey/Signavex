namespace Signavex.Domain.Analysis;

public enum UserProfile
{
    General,
    ConservativeInvestor,
    AggressiveInvestor,
    Homeowner,
    Renter,
    JobSeeker,
    BusinessOwner
}

public static class UserProfileLabels
{
    public static string GetLabel(UserProfile profile) => profile switch
    {
        UserProfile.General => "General",
        UserProfile.ConservativeInvestor => "Conservative Investor",
        UserProfile.AggressiveInvestor => "Aggressive Investor",
        UserProfile.Homeowner => "Homeowner",
        UserProfile.Renter => "Renter",
        UserProfile.JobSeeker => "Job Seeker",
        UserProfile.BusinessOwner => "Business Owner",
        _ => profile.ToString()
    };
}
