using System.Globalization;

namespace SPMobile.Models
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Colors.Gray;
            var status = value.ToString().ToUpper();

            // Background Light Colors
            if (parameter?.ToString() == "Background")
            {
                return status switch
                {
                    "PRESENT" => Color.FromArgb("#E8F5E9"),
                    "LATE" => Color.FromArgb("#FFF3E0"),
                    "ABSENT" => Color.FromArgb("#FFEBEE"),
                    _ => Colors.Transparent
                };
            }

            // Bold Text Colors
            return status switch
            {
                "PRESENT" => Colors.Green,
                "LATE" => Colors.Orange,
                "ABSENT" => Colors.Red,
                _ => Colors.Gray
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}
