namespace DhanMarketData.Calendar;

public class TradingCalendarService
{
    // NSE Trading Holidays 2025-2026
    private readonly HashSet<DateTime> _holidays = new()
    {
        // 2025 Holidays
        new DateTime(2025, 1, 26),  // Republic Day
        new DateTime(2025, 3, 14),  // Maha Shivaratri
        new DateTime(2025, 3, 31),  // Eid-ul-Fitr
        new DateTime(2025, 4, 10),  // Mahavir Jayanti
        new DateTime(2025, 4, 14),  // Dr. Ambedkar Jayanti
        new DateTime(2025, 4, 18),  // Good Friday
        new DateTime(2025, 5, 1),   // Maharashtra Day
        new DateTime(2025, 6, 7),   // Eid-ul-Adha
        new DateTime(2025, 8, 15),  // Independence Day
        new DateTime(2025, 8, 27),  // Ganesh Chaturthi
        new DateTime(2025, 10, 2),  // Mahatma Gandhi Jayanti
        new DateTime(2025, 10, 21), // Dussehra
        new DateTime(2025, 11, 5),  // Diwali Laxmi Pujan
        new DateTime(2025, 11, 24), // Guru Nanak Jayanti
        new DateTime(2025, 12, 25), // Christmas

        // 2026 Holidays (approximate - update when official calendar is released)
        new DateTime(2026, 1, 26),  // Republic Day
        new DateTime(2026, 3, 3),   // Maha Shivaratri
        new DateTime(2026, 3, 21),  // Eid-ul-Fitr
        new DateTime(2026, 3, 30),  // Mahavir Jayanti
        new DateTime(2026, 4, 3),   // Good Friday
        new DateTime(2026, 4, 14),  // Dr. Ambedkar Jayanti
        new DateTime(2026, 5, 1),   // Maharashtra Day
        new DateTime(2026, 5, 28),  // Eid-ul-Adha
        new DateTime(2026, 8, 15),  // Independence Day
        new DateTime(2026, 9, 16),  // Ganesh Chaturthi
        new DateTime(2026, 10, 2),  // Mahatma Gandhi Jayanti
        new DateTime(2026, 10, 9),  // Dussehra
        new DateTime(2026, 10, 24), // Diwali Laxmi Pujan
        new DateTime(2026, 11, 13), // Guru Nanak Jayanti
        new DateTime(2026, 12, 25), // Christmas

        // 2027 Holidays (approximate - update when official NSE calendar is released)
        new DateTime(2027, 1, 26),  // Republic Day
        new DateTime(2027, 2, 21),  // Maha Shivaratri (estimated)
        new DateTime(2027, 3, 11),  // Eid-ul-Fitr (estimated)
        new DateTime(2027, 3, 19),  // Mahavir Jayanti (estimated)
        new DateTime(2027, 3, 26),  // Good Friday
        new DateTime(2027, 4, 14),  // Dr. Ambedkar Jayanti
        new DateTime(2027, 5, 17),  // Eid-ul-Adha (estimated)
        new DateTime(2027, 8, 16),  // Independence Day observance (15th is Sunday)
        new DateTime(2027, 9, 5),   // Ganesh Chaturthi (estimated)
        new DateTime(2027, 10, 7),  // Dussehra (estimated)
        new DateTime(2027, 11, 5),  // Diwali Laxmi Pujan (estimated)
        new DateTime(2027, 12, 3),  // Guru Nanak Jayanti (estimated)
        // Christmas 2027 falls on Saturday — already excluded by weekend rule
    };

    public List<DateTime> GetLastTradingDays(int count)
    {
        var days = new List<DateTime>();
        var current = DateTime.Today;
        
        while (days.Count < count)
        {
            if (IsTradingDay(current))
            {
                days.Add(current);
            }
            current = current.AddDays(-1);
        }
        
        return days;
    }

    public bool IsTradingDay(DateTime date)
    {
        // Skip weekends
        if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            return false;

        // Skip holidays
        if (_holidays.Contains(date.Date))
            return false;

        return true;
    }

    public void AddHoliday(DateTime date)
    {
        _holidays.Add(date.Date);
    }

    public void LoadHolidays(IEnumerable<DateTime> holidays)
    {
        foreach (var holiday in holidays)
        {
            _holidays.Add(holiday.Date);
        }
    }
}
