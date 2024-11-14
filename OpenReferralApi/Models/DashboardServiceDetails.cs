namespace OpenReferralApi.Models;

public class DashboardServiceDetails
{
    public DashboardServiceDetails(ServiceData serviceData)
    {
        Title = serviceData.Name;
        Publisher = serviceData.Publisher;
        ServiceUrl = serviceData.ServiceUrl;
        IsValid = serviceData.StatusIsValid;
        LastTested = serviceData.LastTested;
        Payload = new List<DashboardServiceDetailsPayload>
        {
            new ()
            {
                Label = "MetaData",
                Fields = new List<Field>
                {
                    serviceData.Developer,
                    serviceData.Comment,
                    serviceData.Description
                }
            }
        };
        if (serviceData.OtherDetails != null)
        {
            Payload.Add(new DashboardServiceDetailsPayload
            {
                Label = "Extra details",
                Fields = serviceData.OtherDetails
            });
        }
    }

    public Field Title { get; set; }
    public Field Publisher { get; set; }
    public Field ServiceUrl { get; set; }
    public Field IsValid { get; set; }
    public Field LastTested { get; set; }
    public List<DashboardServiceDetailsPayload> Payload { get; set; }
    
}