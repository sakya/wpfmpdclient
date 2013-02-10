//    WpfMpdClient
//    Copyright (C) 2012, 2013 Paolo Iommarini
//    sakya_tg@yahoo.it
//
//    This program is free software; you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation; either version 2 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program; if not, write to the Free Software
//    Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using System.Timers;
using System.Reflection;
using System.Windows;

namespace WpfMpdClient
{
  public class SearchableListBox : ListBox 
  {
    Timer m_Timer = null;
    string m_Search = string.Empty;
    string m_LastSearch = string.Empty;

    public SearchableListBox()
    {
      m_Timer = new Timer();
      m_Timer.Interval = 250;
      m_Timer.Elapsed += TimerHandler;
      PreviewTextInput += OnPreviewTextInput;
    }

    public PropertyInfo SearchProperty
    {
      get
      {
        return GetValue(SearchPropertyProperty) as PropertyInfo;
      }

      set
      {
        SetValue(SearchPropertyProperty, value);
      }
    }

    public static readonly DependencyProperty SearchPropertyProperty = DependencyProperty.Register(
        "SearchProperty", typeof(PropertyInfo), typeof(SearchableListBox), new PropertyMetadata(null, null));

    private void Search()
    {
      if (Items.Count == 0 || SearchProperty == null)
        return;

      int start = 0;
      if (SelectedItem != null && !string.IsNullOrEmpty(m_LastSearch)) {
        string svalue = SearchProperty.GetValue(SelectedItem, null) as string;
        if (svalue.StartsWith(m_LastSearch, StringComparison.CurrentCultureIgnoreCase))
          start = SelectedIndex;
      }

      for (int index = start; index < Items.Count; index++ ) {
        object item = Items[index];
        string value = SearchProperty.GetValue(item, null) as string;
        if (value != null) {
          if (value.StartsWith(m_Search, StringComparison.CurrentCultureIgnoreCase)) {
            m_LastSearch = m_Search;
            SelectedItem = item;
            ScrollIntoView(item);
            ListBoxItem lbi = ItemContainerGenerator.ContainerFromIndex(SelectedIndex) as ListBoxItem;
            if (lbi != null)
                lbi.Focus();
            break;
          }
        }
      }
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
      m_Timer.Stop();
      m_Search += e.Text;
      Search();
      m_Timer.Start();
      e.Handled = true;
    }

    private void TimerHandler(object sender, ElapsedEventArgs e)
    {
      m_Timer.Stop();
      m_Search = string.Empty;
      m_LastSearch = string.Empty;
    }
  }
}
