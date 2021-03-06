﻿using System.Windows;

namespace ForgeModGenerator.Controls
{
    public class BindingProxy : Freezable
    {
        protected override Freezable CreateInstanceCore() => new BindingProxy();

        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register("Data", typeof(object), typeof(BindingProxy), new PropertyMetadata(null));
        public object Data {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }
    }
}
