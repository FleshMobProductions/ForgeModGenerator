﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ForgeModGenerator.Converters
{
    public class BoolValueConverter<T> : IValueConverter where T : IComparable
    {
        public T TrueValue { get; set; }
        public T FalseValue { get; set; }
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !Invert
                                                                                                    ? (value is bool ? (bool)value ? TrueValue : FalseValue : FalseValue)
                                                                                                    : (value is bool ? (bool)value ? FalseValue : TrueValue : TrueValue);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => !Invert
                                                                                                        ? (value is T val1 ? EqualityComparer<T>.Default.Equals(val1, TrueValue) ? true : false : false)
                                                                                                        : (value is T val2 ? EqualityComparer<T>.Default.Equals(val2, TrueValue) ? false : true : true);
    }

    public class BoolToScrollVisibilityConverter : BoolValueConverter<ScrollBarVisibility>
    {
        public BoolToScrollVisibilityConverter()
        {
            TrueValue = ScrollBarVisibility.Visible;
            FalseValue = ScrollBarVisibility.Hidden;
        }
    }

    public class BoolToTextWrapConverter : BoolValueConverter<TextWrapping>
    {
        public BoolToTextWrapConverter()
        {
            TrueValue = TextWrapping.Wrap;
            FalseValue = TextWrapping.NoWrap;
        }
    }

    public class BoolToVisibilityConverter : BoolValueConverter<Visibility>
    {
        public BoolToVisibilityConverter()
        {
            TrueValue = Visibility.Visible;
            FalseValue = Visibility.Collapsed;
        }
    }
}
