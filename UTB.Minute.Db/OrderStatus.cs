namespace UTB.Minute.Db;

public enum OrderStatus
{
    Preparing = 0,      // Připravuje se
    Ready = 1,          // Hotová
    Cancelled = 2,      // Zrušená
    Completed = 3       // Dokončená
}