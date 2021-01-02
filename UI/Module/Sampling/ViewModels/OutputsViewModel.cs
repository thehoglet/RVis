using LanguageExt;
using MaterialDesignThemes.Wpf;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using ReactiveUI;
using RVis.Base.Extensions;
using RVis.Model;
using RVis.Model.Extensions;
using RVisUI.AppInf.Extensions;
using RVisUI.Model;
using RVisUI.Model.Extensions;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Windows.Input;
using static RVis.Base.Check;
using static RVis.Base.Extensions.LangExt;
using static RVis.Base.Extensions.NumExt;

namespace Sampling
{
  internal sealed class OutputsViewModel : IOutputsViewModel, INotifyPropertyChanged, IDisposable
  {
    internal OutputsViewModel(IAppState appState, IAppService appService, IAppSettings appSettings, ModuleState moduleState)
    {
      _appSettings = appSettings;
      _moduleState = moduleState;
      _simulation = appState.Target.AssertSome();

      _outputsSelectedSampleViewModel = new OutputsSelectedSampleViewModel(appState, appService, moduleState);
      _outputsFilteredSamplesViewModel = new OutputsFilteredSamplesViewModel(appState, appService, moduleState);

      var output = _simulation.SimConfig.SimOutput;

      _outputNames = output.DependentVariables
        .Map(e => e.Name)
        .OrderBy(n => n.ToUpperInvariant())
        .ToArr();

      _moduleState.OutputsState.SelectedOutputName ??= _outputNames.Head();

      _selectedOutputName = _outputNames.IndexOf(
        _moduleState.OutputsState.SelectedOutputName
        );

      Outputs = new PlotModel
      {
        IsLegendVisible = false
      };

      var horizontalAxis = new LinearAxis
      {
        Position = AxisPosition.Bottom,
        Title = output.IndependentVariable.Name,
        Unit = output.IndependentVariable.Unit
      };
      Outputs.Axes.Add(horizontalAxis);

      var verticalAxis = new LinearAxis
      {
        Position = AxisPosition.Left
      };
      Outputs.Axes.Add(verticalAxis);

#pragma warning disable CS0618 // Type or member is obsolete
      Outputs.MouseDown += HandleOutputsMouseDown;
#pragma warning restore CS0618 // Type or member is obsolete

      Outputs.ApplyThemeToPlotModelAndAxes();

      PopulateOutputs();

      ToggleSeriesType = ReactiveCommand.Create(HandleToggleSeriesType);
      ResetAxes = ReactiveCommand.Create(HandleResetAxes);

      _reactiveSafeInvoke = appService.GetReactiveSafeInvoke();

      _subscriptions = new CompositeDisposable(

        appSettings
          .GetWhenPropertyChanged()
          .Subscribe(
            _reactiveSafeInvoke.SuspendAndInvoke<string?>(
              ObserveAppSettingsPropertyChange
              )
            ),

        moduleState.OutputsState
          .ObservableForProperty(os => os.SelectedOutputName)
          .Subscribe(
            _reactiveSafeInvoke.SuspendAndInvoke<object>(
              ObserveOutputsStateSelectedOutputName
              )
            ),

        moduleState.FilteredSamplesState
          .ObservableForProperty(fss => fss.IsEnabled)
          .Subscribe(
            _reactiveSafeInvoke.SuspendAndInvoke<object>(
              ObserveFilteredSamplesStateIsEnabled
              )
            ),

        moduleState
          .ObservableForProperty(ms => ms.Outputs)
          .Subscribe(
            _reactiveSafeInvoke.SuspendAndInvoke<object>(
              ObserveModuleStateOutputs
              )
            ),

        moduleState
          .ObservableForProperty(ms => ms.OutputFilters)
          .Subscribe(
            _reactiveSafeInvoke.SuspendAndInvoke<object>(
              ObserveModuleStateOutputFilters
              )
            ),

        this
          .ObservableForProperty(vm => vm.SelectedOutputName)
          .Subscribe(
            _reactiveSafeInvoke.SuspendAndInvoke<object>(
              ObserveSelectedOutputName
              )
            )

        );
    }

    public bool IsVisible
    {
      get => _isVisible;
      set => this.RaiseAndSetIfChanged(ref _isVisible, value, PropertyChanged);
    }
    private bool _isVisible;

    public Arr<string> OutputNames
    {
      get => _outputNames;
      set => this.RaiseAndSetIfChanged(ref _outputNames, value, PropertyChanged);
    }
    private Arr<string> _outputNames;

    public int SelectedOutputName
    {
      get => _selectedOutputName;
      set => this.RaiseAndSetIfChanged(ref _selectedOutputName, value, PropertyChanged);
    }
    private int _selectedOutputName;

    public PlotModel Outputs { get; }

    public ICommand ToggleSeriesType { get; }

    public bool IsSeriesTypeLine
    {
      get => _isSeriesTypeLine;
      set => this.RaiseAndSetIfChanged(ref _isSeriesTypeLine, value, PropertyChanged);
    }
    private bool _isSeriesTypeLine;

    public ICommand ResetAxes { get; }

    public IOutputsSelectedSampleViewModel OutputsSelectedSampleViewModel => _outputsSelectedSampleViewModel;

    public IOutputsFilteredSamplesViewModel OutputsFilteredSamplesViewModel => _outputsFilteredSamplesViewModel;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Dispose() =>
      Dispose(true);

    private void HandleToggleSeriesType()
    {
      _moduleState.OutputsState.IsSeriesTypeLine = !IsSeriesTypeLine;
      IsSeriesTypeLine = _moduleState.OutputsState.IsSeriesTypeLine;

      Outputs.Series.Clear();
      Outputs.Annotations.Clear();

      PopulateSeries();
      UpdateSelectedSeries(NOT_FOUND, _outputsSelectedSampleViewModel.SelectedSample);

      ApplyOutputFilters();

      Outputs.InvalidatePlot(updateData: true);
    }

    private void HandleResetAxes()
    {
      Outputs.ResetAllAxes();
      Outputs.InvalidatePlot(updateData: false);
    }

    private void HandleOutputsMouseDown(object? sender, OxyMouseDownEventArgs e)
    {
      var indexToUnselect = _outputsSelectedSampleViewModel.SelectedSample;

      var series = Outputs.GetSeriesFromPoint(e.Position);

      var indexToSelect = series is null
        ? NOT_FOUND
        : RequireInstanceOf<int>(series.Tag);

      _outputsSelectedSampleViewModel.SelectedSample = indexToSelect;

      var didUpdate = UpdateSelectedSeries(indexToUnselect, indexToSelect);
      if (didUpdate) Outputs.InvalidatePlot(updateData: false);

      if (series is null) return;

      var xyAxisSeries = RequireInstanceOf<XYAxisSeries>(series);

      var dataPoint = xyAxisSeries.InverseTransform(e.Position);

      if (e.IsControlDown)
      {
        _outputsFilteredSamplesViewModel.SetTo(dataPoint.Y);
      }
      else
      {
        var trackerHitResult = xyAxisSeries.GetNearestPoint(e.Position, interpolate: false);
        var independentVariableValue = trackerHitResult.DataPoint.X;

        var independentVariableName = _simulation.SimConfig.SimOutput.IndependentVariable.Name;
        var (_, output) = _moduleState.Outputs.First();
        var independentData = output[independentVariableName];

        var independentVariableIndex = independentData.Data.FindIndex(d => d == independentVariableValue);

        if (independentVariableIndex.IsntFound()) return;

        _outputsFilteredSamplesViewModel.SetFrom(
          independentVariableIndex,
          independentVariableValue,
          dataPoint.Y
          );
      }
    }

    private void ObserveAppSettingsPropertyChange(string? propertyName)
    {
      if (!propertyName.IsThemeProperty()) return;

      Outputs.ApplyThemeToPlotModelAndAxes();

      if (!_moduleState.Outputs.IsEmpty)
      {
        Outputs.DefaultColors = _appSettings.IsBaseDark
          ? OxyPalettes.Hot(_moduleState.Outputs.Count).Colors
          : OxyPalettes.Cool(_moduleState.Outputs.Count).Colors;
      }

      Outputs.Series.Clear();
      Outputs.Annotations.Clear();

      PopulateSeries();
      ApplyOutputFilters();

      Outputs.InvalidatePlot(updateData: true);
    }

    private void ObserveOutputsStateSelectedOutputName(object _)
    {
      SelectedOutputName = _moduleState.OutputsState.SelectedOutputName.IsAString()
        ? _outputNames.IndexOf(_moduleState.OutputsState.SelectedOutputName)
        : NOT_FOUND;

      PopulateVerticalAxis();

      Outputs.Series.Clear();
      Outputs.Annotations.Clear();

      PopulateSeries();
      UpdateSelectedSeries(NOT_FOUND, _outputsSelectedSampleViewModel.SelectedSample);

      ApplyOutputFilters();

      Outputs.ResetAllAxes();
      Outputs.InvalidatePlot(updateData: true);
    }

    private void ObserveFilteredSamplesStateIsEnabled(object _)
    {
      Outputs.Annotations.Clear();
      ApplyOutputFilters();
      Outputs.InvalidatePlot(updateData: true);
    }

    private void ObserveModuleStateOutputs(object _)
    {
      PopulateOutputs();
    }

    private void ObserveModuleStateOutputFilters(object _)
    {
      Outputs.Annotations.Clear();
      ApplyOutputFilters();
      Outputs.InvalidatePlot(updateData: true);
    }

    private void ObserveSelectedOutputName(object _)
    {
      PopulateVerticalAxis();

      Outputs.Series.Clear();
      Outputs.Annotations.Clear();

      PopulateSeries();
      UpdateSelectedSeries(NOT_FOUND, _outputsSelectedSampleViewModel.SelectedSample);
      
      ApplyOutputFilters();

      Outputs.ResetAllAxes();
      Outputs.InvalidatePlot(updateData: true);

      _moduleState.OutputsState.SelectedOutputName = _selectedOutputName.IsFound()
        ? _outputNames[_selectedOutputName]
        : default;
    }

    private void PopulateVerticalAxis()
    {
      var outputName = _selectedOutputName.IsFound()
        ? _outputNames[_selectedOutputName]
        : default;

      var simValue = outputName.IsAString()
        ? _simulation.SimConfig.SimOutput.FindElement(outputName).AssertSome()
        : default;

      var verticalAxis = Outputs.GetAxis(AxisPosition.Left).AssertNotNull();
      verticalAxis.Title = simValue.Name;
      verticalAxis.Unit = simValue.Unit;
    }

    private void PopulateSeries()
    {
      var outputName = _selectedOutputName.IsFound()
        ? _outputNames[_selectedOutputName]
        : default;

      if (outputName.IsAString())
      {
        var independentVariableName = _simulation.SimConfig.SimOutput.IndependentVariable.Name;

        _moduleState.Outputs.Filter(t => t.Output != default).Iter(t =>
        {
          var isPlotted = Outputs.Series.Any(s => RequireInstanceOf<int>(s.Tag) == t.Index);
          if (isPlotted) return;

          var independentData = t.Output[independentVariableName];
          var dependentData = t.Output[outputName];

          Series series;
          var seriesTitle = $"#{t.Index + 1}";

          if (IsSeriesTypeLine)
          {
            var lineSeries = new LineSeries()
            {
              Title = seriesTitle,
              StrokeThickness = 1,
              LineStyle = LineStyle.Solid,
              Color = Outputs.DefaultColors[Outputs.Series.Count % Outputs.DefaultColors.Count],
              Tag = t.Index
            };

            for (var row = 0; row < independentData.Length; ++row)
            {
              var x = independentData[row];
              var y = dependentData[row];
              var point = new DataPoint(x, y);
              lineSeries.Points.Add(point);
            }

            series = lineSeries;
          }
          else
          {
            var scatterSeries = new ScatterSeries()
            {
              Title = seriesTitle,
              MarkerType = MarkerType.Plus,
              MarkerStroke = Outputs.DefaultColors[Outputs.Series.Count % Outputs.DefaultColors.Count],
              MarkerSize = 1,
              Tag = t.Index
            };

            for (var row = 0; row < independentData.Length; ++row)
            {
              var x = independentData[row];
              var y = dependentData[row];
              var point = new ScatterPoint(x, y);
              scatterSeries.Points.Add(point);
            }

            series = scatterSeries;
          }

          Outputs.Series.Add(series);
        });
      }
    }

    private void ApplyOutputFilters()
    {
      if (!_moduleState.FilteredSamplesState.IsEnabled)
      {
        foreach (var series in Outputs.Series)
        {
          series.IsVisible = true;
        }

        return;
      }

      if (_moduleState.Outputs.IsEmpty) return;
      if (_moduleState.OutputFilters.IsEmpty) return;

      foreach (var series in Outputs.Series)
      {
        var index = RequireInstanceOf<int>(series.Tag);
        var filter = _moduleState.OutputFilters.Find(f => f.Index == index).AssertSome();
        series.IsVisible = filter.IsInFilteredSet;
      }

      if (_selectedOutputName.IsntFound()) return;

      var outputName = _outputNames[_selectedOutputName];

      var independentVariableName = _simulation.SimConfig.SimOutput.IndependentVariable.Name;
      var (_, output) = _moduleState.Outputs.First();
      var independentData = output[independentVariableName];

      var theme = new PaletteHelper().GetTheme();
      var color = OxyColor.FromArgb(
        theme.SecondaryLight.Color.A,
        theme.SecondaryLight.Color.R,
        theme.SecondaryLight.Color.G,
        theme.SecondaryLight.Color.B
        );

      foreach (var filter in _moduleState.FilteredSamplesState.FilteredSampleStates)
      {
        if (!filter.IsEnabled) continue;
        if (filter.OutputName != outputName) continue;

        var lineAnnotation = new LineAnnotation
        {
          Type = LineAnnotationType.Vertical,
          X = independentData[filter.At],
          MinimumY = filter.From,
          MaximumY = filter.To, 
          Color = color, 
          LineStyle = LineStyle.Solid, 
          StrokeThickness = 3
        };

        Outputs.Annotations.Add(lineAnnotation);
      }
    }

    private bool UpdateSelectedSeries(int indexUnselect, int indexSelect)
    {
      var didUpdate = false;

      var seriesToUnselect = Outputs.Series.SingleOrDefault(s => (int)s.Tag == indexUnselect);

      var seriesToSelect = Outputs.Series.SingleOrDefault(s => (int)s.Tag == indexSelect);

      if (seriesToUnselect == seriesToSelect) return didUpdate;

      if (seriesToUnselect is not null)
      {
        if (seriesToUnselect is LineSeries lineSeries)
        {
          lineSeries.MarkerFill = OxyColors.Automatic;
          lineSeries.Color = Outputs.DefaultColors[indexUnselect % Outputs.DefaultColors.Count];
          lineSeries.StrokeThickness = 1;
        }
        else if (seriesToUnselect is ScatterSeries scatterSeries)
        {
          scatterSeries.MarkerFill = Outputs.DefaultColors[indexUnselect % Outputs.DefaultColors.Count];
          scatterSeries.MarkerSize = 1;
        }
        else
        {
          throw new InvalidOperationException(seriesToUnselect.GetType().Name);
        }

        didUpdate = true;
      }

      if (seriesToSelect is not null)
      {
        var theme = new PaletteHelper().GetTheme();
        var color = OxyColor.FromArgb(
          theme.PrimaryMid.Color.A,
          theme.PrimaryMid.Color.R,
          theme.PrimaryMid.Color.G,
          theme.PrimaryMid.Color.B
          );

        if (seriesToSelect is LineSeries lineSeries)
        {
          lineSeries.MarkerFill = color;
          lineSeries.Color = color;
          lineSeries.StrokeThickness = 4;
        }
        else if (seriesToSelect is ScatterSeries scatterSeries)
        {
          scatterSeries.MarkerFill = color;
          scatterSeries.MarkerSize = 4;
        }
        else
        {
          throw new InvalidOperationException(seriesToSelect.GetType().Name);
        }

        didUpdate = true;
      }

      return didUpdate;
    }

    private void PopulateOutputs()
    {
      PopulateVerticalAxis();

      if (_moduleState.Outputs.IsEmpty)
      {
        Outputs.Series.Clear();
        Outputs.Annotations.Clear();
      }
      else
      {
        Outputs.DefaultColors = _appSettings.IsBaseDark
          ? OxyPalettes.Hot(_moduleState.Outputs.Count).Colors
          : OxyPalettes.Cool(_moduleState.Outputs.Count).Colors;
      }

      PopulateSeries();
      ApplyOutputFilters();

      Outputs.ResetAllAxes();
      Outputs.InvalidatePlot(updateData: true);

      _outputsSelectedSampleViewModel.SelectedSample = NOT_FOUND;
    }

    private void Dispose(bool disposing)
    {
      if (!_disposed)
      {
        if (disposing)
        {
          _subscriptions.Dispose();
          _outputsFilteredSamplesViewModel.Dispose();
          _outputsSelectedSampleViewModel.Dispose();
        }

        _disposed = true;
      }
    }

    private readonly IAppSettings _appSettings;
    private readonly ModuleState _moduleState;
    private readonly Simulation _simulation;
    private readonly OutputsSelectedSampleViewModel _outputsSelectedSampleViewModel;
    private readonly OutputsFilteredSamplesViewModel _outputsFilteredSamplesViewModel;
    private readonly IReactiveSafeInvoke _reactiveSafeInvoke;
    private readonly IDisposable _subscriptions;
    private bool _disposed = false;
  }
}
