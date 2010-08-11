#region Copyright (C) 2007-2010 Team MediaPortal

/*
    Copyright (C) 2007-2010 Team MediaPortal
    http://www.team-mediaportal.com
 
    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2.  If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using MediaPortal.Core;
using MediaPortal.Core.Commands;
using MediaPortal.Core.Localization;
using MediaPortal.Core.Logging;
using MediaPortal.Core.MediaManagement.MLQueries;
using MediaPortal.UI.Presentation.DataObjects;
using MediaPortal.UI.Views;
using MediaPortal.UiComponents.Media.FilterCriteria;
using MediaPortal.UiComponents.Media.General;
using MediaPortal.UiComponents.Media.Models.Navigation;

namespace MediaPortal.UiComponents.Media.Models.ScreenData
{
  public abstract class AbstractFiltersScreenData : AbstractScreenData
  {
    protected MLFilterCriterion _filterCriterion;
    protected string _navbarSubViewNavigationDisplayLabel;
    protected bool _avoidClustering = false;

    /// <summary>
    /// Creates a new instance of <see cref="AbstractFiltersScreenData"/>.
    /// </summary>
    /// <param name="screen">The screen associated with this screen data.</param>
    /// <param name="menuItemLabel">Laben which will be shown in the menu to switch to this screen data.</param>
    /// <param name="navbarSubViewNavigationDisplayLabel">Display label to be shown in the navbar when we
    /// navigate to a sub view.</param>
    /// <param name="filterCriterion">Specifies the filter criterion which provides the filter entries for this screen.</param>
    protected AbstractFiltersScreenData(string screen, string menuItemLabel, string navbarSubViewNavigationDisplayLabel,
        MLFilterCriterion filterCriterion) : base(screen, menuItemLabel)
    {
      _navbarSubViewNavigationDisplayLabel = navbarSubViewNavigationDisplayLabel;
      _filterCriterion = filterCriterion;
    }

    public bool AvoidClustering
    {
      get { return _avoidClustering; }
      set { _avoidClustering = value; }
    }

    /// <summary>
    /// Creates a new screen data instance for a sub view.
    /// </summary>
    /// <remarks>
    /// Implementation of this method is necessary to handle value groups.
    /// If value groups are presented to the user and he chooses one of them, the new view will be shown by an instance
    /// similar to this one. This method is responsible for creating the screen data instance for that sub view.
    /// </remarks>
    /// <returns>Screen data instance which looks the same as this view.</returns>
    public abstract AbstractFiltersScreenData Derive();

    public override void CreateScreenData(NavigationData navigationData)
    {
      base.CreateScreenData(navigationData);
      MediaLibraryViewSpecification mlvs = navigationData.BaseViewSpecification as MediaLibraryViewSpecification;
      if (mlvs == null)
      { // Should never happen
        ServiceRegistration.Get<ILogger>().Error("FilterScreenData: Wrong type of media library view '{0}'", navigationData.BaseViewSpecification);
        return;
      }
      CreateFilterValuesList(mlvs);
    }

    /// <summary>
    /// Updates the GUI data for a filter values selection screen which reflects the available filter values of
    /// the given view specification <paramref name="currentVS"/> for our <see cref="_filterCriterion"/>.
    /// </summary>
    /// <param name="currentVS">View specification of the view to be filtered in the current screen.</param>
    protected void CreateFilterValuesList(MediaLibraryViewSpecification currentVS)
    {
      ItemsList items = new ItemsList();

      try
      {
        Display_ListBeingBuilt();
        bool grouping = true;
        int numItems = 0;
        ICollection<FilterValue> fv = _avoidClustering ? null :
            _filterCriterion.GroupValues(currentVS.NecessaryMIATypeIds, currentVS.Filter);
        if (fv != null)
          foreach (FilterValue filterValue in fv)
            numItems += filterValue.NumItems.Value;
        if (fv == null || numItems <= Consts.MAX_NUM_ITEMS_VISIBLE)
        {
          fv = _filterCriterion.GetAvailableValues(currentVS.NecessaryMIATypeIds, currentVS.Filter);
          grouping = false;
        }
        if (fv.Count > Consts.MAX_NUM_ITEMS_VISIBLE)
          Display_TooManyItems(fv.Count);
        else
        {
          List<FilterValue> filterValues = new List<FilterValue>(fv);
          ICollection<AbstractScreenData> remainingScreens = new List<AbstractScreenData>(_navigationData.AvailableScreens);
          remainingScreens.Remove(this);
          filterValues.Sort((f1, f2) => string.Compare(f1.Title, f2.Title));
          foreach (FilterValue filterValue in filterValues)
          {
            string filterTitle = filterValue.Title;
            IFilter combinedFilter = BooleanCombinationFilter.CombineFilters(BooleanOperator.And, new IFilter[] { currentVS.Filter, filterValue.Filter});
            MediaLibraryViewSpecification subVS = currentVS.CreateSubViewSpecification(filterTitle, combinedFilter);
            ListItem filterValueItem = new FilterItem(filterTitle, filterValue.NumItems)
              {
                  Command = grouping ? new MethodDelegateCommand(() => NavigateToGroup(subVS)) :
                      new MethodDelegateCommand(() => NavigateToSubView(subVS, remainingScreens))
              };
            items.Add(filterValueItem);
          }
          Display_Normal(items.Count);
        }
      }
      catch (Exception e)
      {
        ServiceRegistration.Get<ILogger>().Warn("AbstractFiltersScreenData: Error creating filter values list", e);
        Display_ItemsInvalid();
      }
      _items = items;
    }

    protected void NavigateToGroup(ViewSpecification subViewSpecification)
    {
      AbstractFiltersScreenData childScreenData = Derive();
      childScreenData.AvoidClustering = true; // We already showed the clusters in the current screen - avoid clusters again else we would present the same grouped screen contents again
      _navigationData.StackSubordinateNavigationContext(subViewSpecification, childScreenData,
          LocalizationHelper.Translate(_navbarSubViewNavigationDisplayLabel, subViewSpecification.ViewDisplayName));
    }

    protected void NavigateToSubView(ViewSpecification subViewSpecification, ICollection<AbstractScreenData> remainingScreens)
    {
      _navigationData.StackAutonomousNavigationContext(subViewSpecification, remainingScreens,
          LocalizationHelper.Translate(_navbarSubViewNavigationDisplayLabel, subViewSpecification.ViewDisplayName));
    }
  }
}