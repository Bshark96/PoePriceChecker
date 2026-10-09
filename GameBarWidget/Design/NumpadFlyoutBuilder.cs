using System;
using System.Globalization;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace GameBarWidget.Design
{
    public static class NumpadFlyoutBuilder
    {
        public static bool IsFlyoutOpen { get; set; } = false;

        public static Flyout CreateNumpadFlyout(
            Control targetControl,
            Action onValueChanged = null,
            Action onSearchRequested = null)
        {
            var flyout = new Flyout();

            var mainContainer = new StackPanel
            {
                Width = 160,
                Spacing = 5,
                Padding = new Thickness(6),
                Background = DesignPalette.Brush(DesignPalette.SurfaceDark),
                BorderBrush = DesignPalette.Brush(DesignPalette.BorderSubtle),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4)
            };

            Func<string> getText = () =>
            {
                if (targetControl is TextBox tb) return tb.Text ?? string.Empty;
                if (targetControl is PasswordBox pb) return pb.Password ?? string.Empty;
                return string.Empty;
            };

            Action<string> setText = (val) =>
            {
                if (targetControl is TextBox tb) tb.Text = val;
                else if (targetControl is PasswordBox pb) pb.Password = val;
            };

            // 1. Text View Box with Flashing Caret
            string initialText = getText();
            int caretIndex = initialText.Length;

            var displayBox = new Border
            {
                Background = DesignPalette.Brush(Color.FromArgb(255, 11, 18, 27)),
                BorderBrush = DesignPalette.Brush(DesignPalette.BorderInput),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 4, 6, 4),
                MinHeight = 26
            };

            var textStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var leftText = new TextBlock
            {
                Text = initialText,
                FontSize = 11,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = DesignPalette.Brush(DesignPalette.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center
            };

            var caretLine = new Border
            {
                Width = 1.5,
                Height = 12,
                Background = DesignPalette.Brush(DesignPalette.AccentCyan),
                Margin = new Thickness(1, 0, 1, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Visible
            };

            var rightText = new TextBlock
            {
                Text = string.Empty,
                FontSize = 11,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = DesignPalette.Brush(DesignPalette.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center
            };

            textStack.Children.Add(leftText);
            textStack.Children.Add(caretLine);
            textStack.Children.Add(rightText);
            displayBox.Child = textStack;

            mainContainer.Children.Add(displayBox);

            // Flashing Caret Timer
            var caretTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            caretTimer.Tick += (s, e) =>
            {
                caretLine.Visibility = (caretLine.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
            };

            flyout.Opened += (s, e) =>
            {
                caretTimer.Start();
                NumpadFlyoutBuilder.IsFlyoutOpen = true;
            };

            flyout.Closed += (s, e) =>
            {
                caretTimer.Stop();
                NumpadFlyoutBuilder.IsFlyoutOpen = false;
            };

            Action syncTextAndCaret = () =>
            {
                string fullText = getText();
                caretIndex = Math.Max(0, Math.Min(caretIndex, fullText.Length));

                leftText.Text = fullText.Substring(0, caretIndex);
                rightText.Text = fullText.Substring(caretIndex);

                if (onValueChanged != null) onValueChanged();
            };

            syncTextAndCaret();

            // 2. Navigation & Control Buttons (Left, Right, Clear, Del)
            var navGrid = new Grid();
            for (int i = 0; i < 4; i++) navGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftBtn = new Button
            {
                Content = "<",
                FontSize = 10,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(1),
                Padding = new Thickness(0, 3, 0, 3),
                MinHeight = 24,
                Background = DesignPalette.Brush(DesignPalette.SurfaceCard),
                Foreground = DesignPalette.Brush(DesignPalette.TextPrimary),
                CornerRadius = new CornerRadius(3)
            };
            leftBtn.Click += (s, e) =>
            {
                if (caretIndex > 0)
                {
                    caretIndex--;
                    syncTextAndCaret();
                }
            };
            Grid.SetColumn(leftBtn, 0);
            navGrid.Children.Add(leftBtn);

            var rightBtn = new Button
            {
                Content = ">",
                FontSize = 10,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(1),
                Padding = new Thickness(0, 3, 0, 3),
                MinHeight = 24,
                Background = DesignPalette.Brush(DesignPalette.SurfaceCard),
                Foreground = DesignPalette.Brush(DesignPalette.TextPrimary),
                CornerRadius = new CornerRadius(3)
            };
            rightBtn.Click += (s, e) =>
            {
                string full = getText();
                if (caretIndex < full.Length)
                {
                    caretIndex++;
                    syncTextAndCaret();
                }
            };
            Grid.SetColumn(rightBtn, 1);
            navGrid.Children.Add(rightBtn);

            var clearBtn = new Button
            {
                Content = "CLEAR",
                FontSize = 8,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(1),
                Padding = new Thickness(0, 3, 0, 3),
                MinHeight = 24,
                Background = DesignPalette.Brush(Color.FromArgb(255, 60, 20, 20)),
                Foreground = DesignPalette.Brush(DesignPalette.AccentRed),
                CornerRadius = new CornerRadius(3)
            };
            clearBtn.Click += (s, e) =>
            {
                setText(string.Empty);
                caretIndex = 0;
                syncTextAndCaret();
            };
            Grid.SetColumn(clearBtn, 2);
            navGrid.Children.Add(clearBtn);

            var delBtn = new Button
            {
                Content = "DEL",
                FontSize = 8.5,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(1),
                Padding = new Thickness(0, 3, 0, 3),
                MinHeight = 24,
                Background = DesignPalette.Brush(DesignPalette.SurfaceCard),
                Foreground = DesignPalette.Brush(DesignPalette.AccentAmber),
                CornerRadius = new CornerRadius(3)
            };
            delBtn.Click += (s, e) =>
            {
                string full = getText();
                if (caretIndex > 0 && full.Length > 0)
                {
                    setText(full.Remove(caretIndex - 1, 1));
                    caretIndex--;
                    syncTextAndCaret();
                }
            };
            Grid.SetColumn(delBtn, 3);
            navGrid.Children.Add(delBtn);

            mainContainer.Children.Add(navGrid);

            // 3. Numbers Grid (1-9, 0, dot)
            var padGrid = new Grid();
            for (int r = 0; r < 4; r++) padGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int c = 0; c < 3; c++) padGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            string[,] keys = new string[,]
            {
                { "7", "8", "9" },
                { "4", "5", "6" },
                { "1", "2", "3" },
                { "0", ".", "-" }
            };

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    string keyVal = keys[r, c];
                    var btn = new Button
                    {
                        Content = keyVal,
                        FontSize = 11,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Margin = new Thickness(1),
                        Padding = new Thickness(0, 4, 0, 4),
                        MinHeight = 26,
                        Background = DesignPalette.Brush(DesignPalette.SurfaceCard),
                        Foreground = DesignPalette.Brush(DesignPalette.TextPrimary),
                        CornerRadius = new CornerRadius(3)
                    };

                    btn.Click += (s, e) =>
                    {
                        string full = getText();
                        if (keyVal == ".")
                        {
                            if (!full.Contains("."))
                            {
                                setText(full.Insert(caretIndex, "."));
                                caretIndex++;
                            }
                        }
                        else if (keyVal == "-")
                        {
                            if (full.StartsWith("-"))
                            {
                                setText(full.Substring(1));
                                caretIndex = Math.Max(0, caretIndex - 1);
                            }
                            else
                            {
                                setText("-" + full);
                                caretIndex++;
                            }
                        }
                        else
                        {
                            setText(full.Insert(caretIndex, keyVal));
                            caretIndex += keyVal.Length;
                        }
                        syncTextAndCaret();
                    };

                    Grid.SetRow(btn, r);
                    Grid.SetColumn(btn, c);
                    padGrid.Children.Add(btn);
                }
            }

            mainContainer.Children.Add(padGrid);

            // 4. Bottom OK Button
            var okBtn = new Button
            {
                Content = "OK",
                FontSize = 10,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(1, 2, 1, 1),
                Padding = new Thickness(0, 4, 0, 4),
                MinHeight = 26,
                Background = DesignPalette.Brush(Color.FromArgb(255, 14, 116, 144)),
                Foreground = DesignPalette.Brush(DesignPalette.TextPrimary),
                CornerRadius = new CornerRadius(3)
            };
            okBtn.Click += (s, e) =>
            {
                flyout.Hide();
                if (onSearchRequested != null) onSearchRequested();
            };

            mainContainer.Children.Add(okBtn);

            flyout.Content = mainContainer;
            return flyout;
        }

        public static void AttachNumpadContextMenu(
            Control targetControl,
            Action onValueChanged = null,
            Action onSearchRequested = null,
            Action<MenuFlyoutSubItem> populateModifiersSubTab = null,
            Action<MenuFlyout> populateCustomActions = null)
        {
            var menuFlyout = new MenuFlyout();

            menuFlyout.Opened += (s, e) =>
            {
                IsFlyoutOpen = true;
            };

            menuFlyout.Closed += (s, e) =>
            {
                IsFlyoutOpen = false;
            };

            // 1. Numpad Launch Action
            var openNumpadItem = new MenuFlyoutItem { Text = "Show Visual Numpad" };
            openNumpadItem.Click += (s, e) =>
            {
                var padFlyout = CreateNumpadFlyout(targetControl, onValueChanged, onSearchRequested);
                padFlyout.ShowAt(targetControl);
            };
            menuFlyout.Items.Add(openNumpadItem);

            menuFlyout.Items.Add(new MenuFlyoutSeparator());

            // 2. Modifiers SubTab
            if (populateModifiersSubTab != null)
            {
                var modifiersSubMenu = new MenuFlyoutSubItem { Text = "Modifiers" };
                populateModifiersSubTab(modifiersSubMenu);
                if (modifiersSubMenu.Items.Count > 0)
                {
                    menuFlyout.Items.Add(modifiersSubMenu);
                    menuFlyout.Items.Add(new MenuFlyoutSeparator());
                }
            }

            // 3. Custom Actions
            if (populateCustomActions != null)
            {
                populateCustomActions(menuFlyout);
                menuFlyout.Items.Add(new MenuFlyoutSeparator());
            }

            // 4. Default Text / Clipboard Actions
            if (targetControl is TextBox tb)
            {
                var cutItem = new MenuFlyoutItem { Text = "Cut" };
                cutItem.Click += (s, e) => { tb.CutSelectionToClipboard(); };
                menuFlyout.Items.Add(cutItem);

                var copyItem = new MenuFlyoutItem { Text = "Copy" };
                copyItem.Click += (s, e) => { tb.CopySelectionToClipboard(); };
                menuFlyout.Items.Add(copyItem);

                var pasteItem = new MenuFlyoutItem { Text = "Paste" };
                pasteItem.Click += (s, e) => { tb.PasteFromClipboard(); };
                menuFlyout.Items.Add(pasteItem);

                var selectAllItem = new MenuFlyoutItem { Text = "Select All" };
                selectAllItem.Click += (s, e) => { tb.SelectAll(); };
                menuFlyout.Items.Add(selectAllItem);
            }
            else if (targetControl is PasswordBox pb)
            {
                var pasteItem = new MenuFlyoutItem { Text = "Paste" };
                pasteItem.Click += (s, e) =>
                {
                    try
                    {
                        var data = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
                        if (data.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
                        {
                            _ = Task.Run(async () =>
                            {
                                string text = await data.GetTextAsync();
                                await pb.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                {
                                    pb.Password = text;
                                });
                            });
                        }
                    }
                    catch { }
                };
                menuFlyout.Items.Add(pasteItem);

                var selectAllItem = new MenuFlyoutItem { Text = "Select All" };
                selectAllItem.Click += (s, e) => { pb.SelectAll(); };
                menuFlyout.Items.Add(selectAllItem);
            }

            targetControl.ContextFlyout = menuFlyout;

            targetControl.DoubleTapped += (s, e) =>
            {
                var padFlyout = CreateNumpadFlyout(targetControl, onValueChanged, onSearchRequested);
                padFlyout.ShowAt(targetControl);
            };
        }
    }
}
