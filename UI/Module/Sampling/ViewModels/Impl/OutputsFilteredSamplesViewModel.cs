using ReactiveUI;
using RVis.Base.Extensions;
using RVis.Model;
using RVis.Model.Extensions;
using RVisUI.Model;
using RVisUI.Model.Extensions;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Windows.Input;
using static RVis.Base.Check;
using static System.Globalization.CultureInfo;

namespace Sampling
{
  internal sealed class OutputsFilteredSamplesViewModel : IOutputsFilteredSamplesViewModel, INotifyPropertyChanged, IDisposable
  {
    internal OutputsFilteredSamplesViewModel(IAppState appState, IAppService appService, ModuleState moduleState)
    {
      _moduleState = moduleState;

      _simulation = appState.Target.AssertSome();

      _toggleEnable = ReactiveCommand.Create<OutputsFilterViewModel>(HandleToggleEnable);
      _delete = ReactiveCommand.Create<OutputsFilterViewModel>(HandleDelete);

      var independentVariable = _simulation.SimConfig.SimOutput.IndependentVariable;
      IndependentVariableName = independentVariable.Name;
      IndependentVariableUnit = independentVariable.Unit;

      _outputName = moduleState.OutputsState.SelectedOutputName;

      AddNewFilter = ReactiveCommand.Create(
        HandleAddNewFilter,
        this.WhenAny(
          vm => vm.FromN,
          vm => vm.ToN,
          (_, _) => FromN.HasValue && ToN.HasValue
          )
        );

      IsUnion = moduleState.FilteredSamplesState.IsUnion;
      IsEnabled = moduleState.FilteredSamplesState.IsEnabled;
      PopulateOutputFilters();

      _reactiveSafeInvoke = appService.GetReactiveSafeInvoke();

      _subscriptions = new CompositeDisposable(

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

        moduleState.OutputsState
          .ObservableForProperty(os => os.SelectedOutputName)
          .Subscribe(
            _reactiveSafeInvoke.SuspendAndInvoke<object>(
              ObserveOutputsStateSelectedOutputName
              )
            ),

        this
          .ObservableForProperty(vm => vm.FromT)
          .Subscribe(
            _reactiveSafeInvoke.SuspendAndInvoke<object>(
              ObserveFromT
              )
            ),

        this
          .ObservableForProperty(vm => vm.ToT)
          .Subscribe(
            _reactiveSafeInvoke.SuspendAndInvoke<object>(
              ObserveToT
              )
            ),

        this
          .ObservableForProperty(vm => vm.IsEnabled)
          .Subscribe(
            _reactiveSafeInvoke.SuspendAndInvoke<object>(
              ObserveIsEnabled
              )
            ),

        this
          .ObservableForProperty(vm => vm.IsUnion)
          .Subscribe(
            _reactiveSafeInvoke.SuspendAndInvoke<object>(
              ObserveIsUnion
              )
            )

        );
    }

    public string IndependentVariableName { get; }

    public int? IndependentVariableIndex
    {
      get => _independentVariableIndex;
      set => this.RaiseAndSetIfChanged(ref _independentVariableIndex, value, PropertyChanged);
    }
    private int? _independentVariableIndex;

    public double? IndependentVariableValue
    {
      get => _independentVariableValue;
      set => this.RaiseAndSetIfChanged(ref _independentVariableValue, value, PropertyChanged);
    }
    private double? _independentVariableValue;

    public string? IndependentVariableUnit { get; }

    public string? OutputName
    {
      get => _outputName;
      set => this.RaiseAndSetIfChanged(ref _outputName, value, PropertyChanged);
    }
    private string? _outputName;

    public double? FromN
    {
      get => _fromN;
      set => this.RaiseAndSetIfChanged(ref _fromN, value, PropertyChanged);
    }
    private double? _fromN;

    public string? FromT
    {
      get => _fromT;
      set => this.RaiseAndSetIfChanged(ref _fromT, value, PropertyChanged);
    }
    private string? _fromT;

    public double? ToN
    {
      get => _toN;
      set => this.RaiseAndSetIfChanged(ref _toN, value, PropertyChanged);
    }
    private double? _toN;

    public string? ToT
    {
      get => _toT;
      set => this.RaiseAndSetIfChanged(ref _toT, value, PropertyChanged);
    }
    private string? _toT;

    public ICommand AddNewFilter { get; }

    public ObservableCollection<IOutputsFilterViewModel> OutputsFilterViewModels { get; } = new();

    public bool IsUnion
    {
      get => _isUnion;
      set => this.RaiseAndSetIfChanged(ref _isUnion, value, PropertyChanged);
    }
    private bool _isUnion;

    public bool IsEnabled
    {
      get => _isEnabled;
      set => this.RaiseAndSetIfChanged(ref _isEnabled, value, PropertyChanged);
    }
    private bool _isEnabled;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Dispose() =>
      Dispose(true);

    public void SetFrom(int independentVariableIndex, double independentVariableValue, double from)
    {
      using (_reactiveSafeInvoke.SuspendedReactivity)
      {
        IndependentVariableIndex = independentVariableIndex;
        IndependentVariableValue = independentVariableValue;
        FromN = from;
        FromT = from.ToString(InvariantCulture);
        ToN = default;
        ToT = default;
      }
    }

    public void SetTo(double to)
    {
      if (!FromN.HasValue) return;

      using (_reactiveSafeInvoke.SuspendedReactivity)
      {
        ToN = to;
        ToT = to.ToString(InvariantCulture);
      }
    }

    private void HandleToggleEnable(OutputsFilterViewModel outputsFilterViewModel)
    {
      using (_reactiveSafeInvoke.SuspendedReactivity)
      {
        var filteredSampleStates = _moduleState.FilteredSamplesState.FilteredSampleStates;

        var index = filteredSampleStates.FindIndex(
          fss => fss.GetHashCode() == outputsFilterViewModel.FilterHashCode
          );

        RequireTrue(index.IsFound());

        var filteredSampleState = filteredSampleStates[index];
        filteredSampleState = filteredSampleState with { IsEnabled = !filteredSampleState.IsEnabled };

        outputsFilterViewModel.FilterHashCode = filteredSampleState.GetHashCode();

        filteredSampleStates = filteredSampleStates.SetItem(index, filteredSampleState);

        _moduleState.FilteredSamplesState.FilteredSampleStates = filteredSampleStates;

        RecompileOutputFilters();
      }
    }

    private void HandleDelete(OutputsFilterViewModel outputsFilterViewModel)
    {
      using (_reactiveSafeInvoke.SuspendedReactivity)
      {
        var filteredSampleStates = _moduleState.FilteredSamplesState.FilteredSampleStates;

        var index = filteredSampleStates.FindIndex(
          fss => fss.GetHashCode() == outputsFilterViewModel.FilterHashCode
          );

        RequireTrue(index.IsFound());

        filteredSampleStates = filteredSampleStates.RemoveAt(index);

        _moduleState.FilteredSamplesState.FilteredSampleStates = filteredSampleStates;

        RecompileOutputFilters();

        OutputsFilterViewModels.Remove(outputsFilterViewModel);
      }
    }

    private void HandleAddNewFilter()
    {
      using (_reactiveSafeInvoke.SuspendedReactivity)
      {
        RequireNotNull(IndependentVariableValue);
        RequireNotNull(IndependentVariableIndex);
        RequireNotNullEmptyWhiteSpace(OutputName);
        RequireNotNull(FromN);
        RequireNotNull(ToN);

        var (from, to) = FromN > ToN
          ? (ToN.Value, FromN.Value)
          : (FromN.Value, ToN.Value);

        var filteredSampleState = new FilteredSampleState(
          OutputName,
          from,
          to,
          IndependentVariableIndex.Value,
          IsEnabled: true
          );

        var filteredSampleStates = _moduleState.FilteredSamplesState.FilteredSampleStates;
        filteredSampleStates = filteredSampleStates.Add(filteredSampleState);
        _moduleState.FilteredSamplesState.FilteredSampleStates = filteredSampleStates;

        RecompileOutputFilters();

        var element = _simulation.SimConfig.SimOutput.FindElement(OutputName).AssertSome();

        OutputsFilterViewModels.Add(new OutputsFilterViewModel(
          IndependentVariableName,
          IndependentVariableValue.Value,
          IndependentVariableUnit,
          OutputName,
          from,
          to,
          element.Unit,
          _toggleEnable,
          _delete
          )
        { IsEnabled = true, FilterHashCode = filteredSampleState.GetHashCode() });

        IndependentVariableValue = default;
        FromN = default;
        FromT = default;
        ToN = default;
        ToT = default;
      }
    }

    private void ObserveModuleStateOutputs(object _)
    {
      PopulateOutputFilters();
    }

    private void ObserveModuleStateOutputFilters(object _)
    {
      PopulateOutputFilters();
    }

    private void ObserveOutputsStateSelectedOutputName(object _)
    {
      OutputName = _moduleState.OutputsState.SelectedOutputName;
    }

    private void ObserveFromT(object _)
    {
      FromN = double.TryParse(_fromT, out double d)
        ? d
        : default(double?);
    }

    private void ObserveToT(object _)
    {
      ToN = double.TryParse(_toT, out double d)
        ? d
        : default(double?);
    }

    private void ObserveIsEnabled(object _)
    {
      _moduleState.FilteredSamplesState.IsEnabled = _isEnabled;
    }

    private void ObserveIsUnion(object _)
    {
      _moduleState.FilteredSamplesState.IsUnion = _isUnion;
      RecompileOutputFilters();
    }

    private void RecompileOutputFilters()
    {
      _moduleState.OutputFilters = _moduleState.Outputs.Map(
        t => (t.Index, _moduleState.FilteredSamplesState.IsInFilteredSet(t.Output))
        );
    }

    private void PopulateOutputFilters()
    {
      OutputsFilterViewModels.Clear();

      if (_moduleState.Outputs.IsEmpty) return;

      var independentVariableName = _simulation.SimConfig.SimOutput.IndependentVariable.Name;
      var (_, output) = _moduleState.Outputs.First();
      var independentData = output[independentVariableName];

      string? GetOutputUnit(string output)
      {
        var element = _simulation.SimConfig.SimOutput.FindElement(output).AssertSome();
        return element.Unit;
      }

      _moduleState.FilteredSamplesState.FilteredSampleStates.Iter(fss =>
      {
        var outputsFilterViewModel = new OutputsFilterViewModel(
          IndependentVariableName,
          independentData[fss.At],
          IndependentVariableUnit,
          fss.OutputName,
          fss.From,
          fss.To,
          GetOutputUnit(fss.OutputName),
          _toggleEnable,
          _delete
          )
        { IsEnabled = fss.IsEnabled, FilterHashCode = fss.GetHashCode() };

        OutputsFilterViewModels.Add(outputsFilterViewModel);
      });
    }

    private void Dispose(bool disposing)
    {
      if (!_disposed)
      {
        if (disposing)
        {
          _subscriptions.Dispose();
        }

        _disposed = true;
      }
    }

    private bool _disposed = false;
    private readonly ModuleState _moduleState;
    private readonly Simulation _simulation;
    private readonly ICommand _toggleEnable;
    private readonly ICommand _delete;
    private readonly IReactiveSafeInvoke _reactiveSafeInvoke;
    private readonly IDisposable _subscriptions;
  }
}
