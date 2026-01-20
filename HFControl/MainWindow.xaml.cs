using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using HFControl.Services;

namespace HFControl;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly FuzzyFurnaceController _controller = new();
    private readonly DispatcherTimer _simulationTimer = new();
    private readonly List<double> _temperatureHistory = [];
    private readonly List<double> _outputHistory = [];
    
    private double _currentTemperature = 18.0;
    private double _outsideTemperature = 5.0;
    private double _heatLossRate = 0.5;
    private int _simulationSeconds;
    private bool _isRunning;

    private const int MaxHistoryPoints = 200;
    private const double MaxFurnaceHeatingRate = 3.0;

    public MainWindow()
    {
        InitializeComponent();
        
        _simulationTimer.Interval = TimeSpan.FromMilliseconds(100);
        _simulationTimer.Tick += SimulationTimer_Tick;
        
        _controller.TargetTemperature = TargetTempSlider.Value;
        UpdateDisplay();
    }
    
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeButton_Click(sender, e);
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized 
            ? WindowState.Normal 
            : WindowState.Maximized;
        
        // Update maximize button icon
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        _isRunning = true;
        _simulationTimer.Start();
        
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        
        StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80));
        StatusText.Text = "Simulation running...";
        SimStatusText.Text = "Running";
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _isRunning = false;
        _simulationTimer.Stop();
        
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        
        StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 183, 77));
        StatusText.Text = "Simulation paused";
        SimStatusText.Text = "Paused";
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _simulationTimer.Stop();
        _isRunning = false;
        
        _currentTemperature = _outsideTemperature + 5; // Start slightly above outside temp
        _simulationSeconds = 0;
        _temperatureHistory.Clear();
        _outputHistory.Clear();
        _controller.Reset();
        
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        
        StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(124, 124, 140));
        StatusText.Text = "Simulation reset - Press Start to begin";
        SimStatusText.Text = "Stopped";
        
        UpdateDisplay();
        DrawGraph();
    }

    private void TargetTempSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TargetTempText == null) return;
        
        _controller.TargetTemperature = e.NewValue;
        TargetTempText.Text = e.NewValue.ToString("F1");
    }

    private void OutsideTempSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OutsideTempText == null) return;
        
        _outsideTemperature = e.NewValue;
        OutsideTempText.Text = e.NewValue.ToString("F1");
    }

    private void HeatLossSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (HeatLossText == null) return;
        
        _heatLossRate = e.NewValue;
        HeatLossText.Text = e.NewValue.ToString("F1");
    }

    private void SimulationTimer_Tick(object? sender, EventArgs e)
    {
        _simulationSeconds++;
        
        // Calculate furnace output using fuzzy controller
        var output = _controller.CalculateOutput(_currentTemperature);
        
        // Simulate temperature change
        var heatingEffect = (output / 100.0) * MaxFurnaceHeatingRate / 60.0; // Per tick (1 simulated minute / 60)
        var coolingEffect = (_currentTemperature - _outsideTemperature) * (_heatLossRate / 60.0) / 10.0;
        
        _currentTemperature += heatingEffect - coolingEffect;
        
        // Store history
        _temperatureHistory.Add(_currentTemperature);
        _outputHistory.Add(output);
        
        if (_temperatureHistory.Count > MaxHistoryPoints)
        {
            _temperatureHistory.RemoveAt(0);
            _outputHistory.RemoveAt(0);
        }
        
        UpdateDisplay();
        DrawGraph();
    }

    private void UpdateDisplay()
    {
        var output = _isRunning ? _controller.CalculateOutput(_currentTemperature) : 0;
        
        CurrentTempText.Text = _currentTemperature.ToString("F1");
        OutputText.Text = output.ToString("F0");
        OutputProgressBar.Value = output;
        
        var minutes = _simulationSeconds / 60;
        var seconds = _simulationSeconds % 60;
        SimTimeText.Text = $"{minutes:D2}:{seconds:D2}";
        
        // Update temperature status
        var error = _controller.TargetTemperature - _currentTemperature;
        if (Math.Abs(error) < 0.5)
        {
            TempStatusText.Text = "At target ✓";
            TempStatusText.Foreground = new SolidColorBrush(Color.FromRgb(129, 199, 132));
        }
        else if (error > 0)
        {
            TempStatusText.Text = $"Below target ({error:F1}°C)";
            TempStatusText.Foreground = new SolidColorBrush(Color.FromRgb(79, 195, 247));
        }
        else
        {
            TempStatusText.Text = $"Above target ({-error:F1}°C)";
            TempStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 112, 67));
        }
        
        // Update output progress bar color based on output level
        OutputProgressBar.Foreground = output switch
        {
            > 75 => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
            > 50 => new SolidColorBrush(Color.FromRgb(255, 112, 67)),
            > 25 => new SolidColorBrush(Color.FromRgb(255, 183, 77)),
            _ => new SolidColorBrush(Color.FromRgb(129, 199, 132))
        };
    }

    private void DrawGraph()
    {
        GraphCanvas.Children.Clear();

        if (_temperatureHistory.Count < 2) return;

        var width = GraphCanvas.ActualWidth;
        var height = GraphCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Draw grid lines
        for (int i = 0; i <= 4; i++)
        {
            var y = height * i / 4;
            var gridLine = new Line
            {
                X1 = 0, Y1 = y, X2 = width, Y2 = y,
                Stroke = new SolidColorBrush(Color.FromRgb(60, 60, 80)),
                StrokeThickness = 1
            };
            GraphCanvas.Children.Add(gridLine);
        }

        // Calculate scale
        var minTemp = Math.Min(_temperatureHistory.Min(), _outsideTemperature) - 5;
        var maxTemp = Math.Max(_temperatureHistory.Max(), _controller.TargetTemperature) + 5;
        var tempRange = maxTemp - minTemp;

        // Draw target temperature line
        var targetY = height - (((_controller.TargetTemperature - minTemp) / tempRange) * height);
        var targetLine = new Line
        {
            X1 = 0, Y1 = targetY, X2 = width, Y2 = targetY,
            Stroke = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            StrokeThickness = 2,
            StrokeDashArray = [5, 3]
        };
        GraphCanvas.Children.Add(targetLine);

        // Draw temperature line
        var tempPoints = new PointCollection();
        for (int i = 0; i < _temperatureHistory.Count; i++)
        {
            var x = (i / (double)MaxHistoryPoints) * width;
            var y = height - (((_temperatureHistory[i] - minTemp) / tempRange) * height);
            tempPoints.Add(new Point(x, y));
        }

        var tempPolyline = new Polyline
        {
            Points = tempPoints,
            Stroke = new SolidColorBrush(Color.FromRgb(79, 195, 247)),
            StrokeThickness = 2
        };
        GraphCanvas.Children.Add(tempPolyline);

        // Draw output line (scaled to fit)
        var outputPoints = new PointCollection();
        for (int i = 0; i < _outputHistory.Count; i++)
        {
            var x = (i / (double)MaxHistoryPoints) * width;
            var y = height - ((_outputHistory[i] / 100.0) * height * 0.3); // Scale to 30% of height
            outputPoints.Add(new Point(x, y));
        }

        var outputPolyline = new Polyline
        {
            Points = outputPoints,
            Stroke = new SolidColorBrush(Color.FromRgb(255, 112, 67)),
            StrokeThickness = 1.5,
            Opacity = 0.7
        };
        GraphCanvas.Children.Add(outputPolyline);
    }
}