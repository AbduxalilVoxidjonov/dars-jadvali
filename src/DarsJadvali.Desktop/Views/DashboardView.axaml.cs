using Avalonia.Controls;

namespace DarsJadvali.Desktop.Views;

/// <summary>
/// Bosh sahifa. Butun UI deklarativ — <c>DashboardView.axaml</c> da.
/// </summary>
/// <remarks>
/// M-04 yopildi: bu yerda avval maktab jadvalini <b>319 qator</b> imperativ kod bilan quruvchi
/// <c>BuildTimetable()</c> bo'lgan (qat'iy piksel ustunlar 136/104/150, virtualizatsiyasiz,
/// har o'zgarishda butun daraxtni qayta qurish). To'r endi
/// <c>TimetableBoardView</c> ga ko'chirilgan: deklarativ, virtualizatsiyalangan va tahrirlanadigan.
/// </remarks>
public partial class DashboardView : UserControl
{
    /// <summary>Ekranni yaratadi.</summary>
    public DashboardView()
    {
        InitializeComponent();
    }
}
