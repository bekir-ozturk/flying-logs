public class Book
{
    public int Id { get; set; }
    public string? Isbn { get; set; }
    public DateTime PublishingDate { get; set; }
    public Publisher? Publisher { get; set; }
}

public class Publisher
{
    public string Name { get; set; }
    public Address? Address { get; set; }
}

public class Address
{
    public string? StreetName { get; set; }
    public CountryOrRegion CountryOrRegion { get; set; }
}

public enum CountryOrRegion
{
    Italy,
    Greece,
    Japan
}
