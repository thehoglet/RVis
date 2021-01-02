using LanguageExt;
using RVis.Data;
using RVisUI.Model.Extensions;
using System.ComponentModel;

namespace Sampling
{
  internal sealed class FilteredSamplesState : INotifyPropertyChanged
  {
    internal bool IsEnabled
    {
      get => _isEnabled;
      set => this.RaiseAndSetIfChanged(ref _isEnabled, value, PropertyChanged);
    }
    private bool _isEnabled;

    internal bool IsUnion
    {
      get => _isUnion;
      set => this.RaiseAndSetIfChanged(ref _isUnion, value, PropertyChanged);
    }
    private bool _isUnion;

    internal Arr<FilteredSampleState> FilteredSampleStates
    {
      get => _filteredSampleStates;
      set => this.RaiseAndSetIfChanged(ref _filteredSampleStates, value, PropertyChanged);
    }
    private Arr<FilteredSampleState> _filteredSampleStates;

    internal bool IsInFilteredSet(NumDataTable output)
    {
      var filters = FilteredSampleStates
        .Filter(fss => fss.IsEnabled)
        .Map(fss =>
        {
          var column = output[fss.OutputName];
          var datum = column[fss.At];
          return datum >= fss.From && datum <= fss.To;
        });

      if (filters.IsEmpty) return true;

      return IsUnion ? filters.Exists(f => f) : filters.ForAll(f => f);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
  }
}
