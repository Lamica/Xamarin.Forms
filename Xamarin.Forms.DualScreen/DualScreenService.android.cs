﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Util;
using Android.Views;
using Microsoft.Device.Display;
using Xamarin.Forms;
using Xamarin.Forms.DualScreen;
using Xamarin.Forms.Internals;
using Xamarin.Forms.Platform.Android;

[assembly: Dependency(typeof(DualScreenService.DualScreenServiceImpl))]

namespace Xamarin.Forms.DualScreen
{
	public class DualScreenService
	{
		public static void Init(Activity activity)
		{
			DependencyService.Register<DualScreenServiceImpl>();
			DualScreenServiceImpl.Init(activity);
		}

		internal class DualScreenServiceImpl : IDualScreenService, IDisposable
		{
			public event EventHandler OnScreenChanged;
			ScreenHelper _helper;
			bool _isDuo = false;
			HingeSensor _hingeSensor;
			static Activity _mainActivity;
			static DualScreenServiceImpl _HingeService;

			int _hingeAngle;
			Rectangle _hingeLocation;
			bool _isLandscape;
			Size _pixelScreenSize;

			Activity MainActivity
			{
				get => _mainActivity;
				set => _mainActivity = value;
			}

			public DualScreenServiceImpl()
			{
				_HingeService = this;
				if (_mainActivity != null)
					Init(_mainActivity);
			}

			public static void Init(Activity activity)
			{
				if (_HingeService == null)
				{
					_mainActivity = activity;
					return;
				}

				if (activity == _HingeService.MainActivity && _HingeService._helper != null)
					return;

				if (_mainActivity is IDeviceInfoProvider oldDeviceInfoProvider)
				{
					oldDeviceInfoProvider.ConfigurationChanged -= _HingeService.ConfigurationChanged;
				}

				_mainActivity = activity;

				if (_mainActivity is IDeviceInfoProvider newDeviceInfoProvider)
				{
					newDeviceInfoProvider.ConfigurationChanged += _HingeService.ConfigurationChanged;
				}

				if (_HingeService._helper == null)
				{
					_HingeService._helper = new ScreenHelper();
				}

				if (_HingeService._hingeSensor != null)
				{
					//_HingeService._hingeSensor.OnSensorChanged -= _HingeService.OnSensorChanged;
					//_HingeService._hingeSensor.StopListening();
				}

				_HingeService._isDuo = _HingeService._helper.Initialize(_HingeService.MainActivity);

				if (_HingeService._isDuo)
				{
					_HingeService._hingeSensor = new HingeSensor(_HingeService.MainActivity);
					//_HingeService._hingeSensor.OnSensorChanged += _HingeService.OnSensorChanged;
					//_HingeService._hingeSensor.StartListening();
				}
			}

			void ConfigurationChanged(object sender, EventArgs e)
			{
				bool screenChanged = false;
				if (_isLandscape != IsLandscape)
				{
					_isLandscape = IsLandscape;
					screenChanged = true;
				}

				if (_mainActivity != null)
				{
					using (DisplayMetrics display = _mainActivity.Resources.DisplayMetrics)
					{
						var scalingFactor = display.Density;
						_pixelScreenSize = new Size(display.WidthPixels, display.HeightPixels);
						var newSize = new Size(_pixelScreenSize.Width / scalingFactor, _pixelScreenSize.Height / scalingFactor);

						if (newSize != ScaledScreenSize)
						{
							ScaledScreenSize = newSize;
							screenChanged = true;
						}
					}
				}

				if(screenChanged)
					OnScreenChanged?.Invoke(this, e);
			}

			void OnSensorChanged(object sender, HingeSensor.HingeSensorChangedEventArgs e)
			{
				if (_hingeLocation != GetHinge())
				{
					_hingeLocation = GetHinge();
				}

				if (_hingeAngle != e.HingeAngle)
					OnScreenChanged?.Invoke(this, EventArgs.Empty);

				_hingeAngle = e.HingeAngle;
			}

			public void Dispose()
			{
				if (_hingeSensor != null)
				{
					//_hingeSensor.OnSensorChanged -= OnSensorChanged;
					//_hingeSensor.StopListening();
				}
			}

			public Size ScaledScreenSize
			{
				get;
				private set;
			}

			public bool IsSpanned
				=> _isDuo && (_helper?.IsDualMode ?? false);

			public Rectangle GetHinge()
			{
				if (!_isDuo || _helper == null)
					return Rectangle.Zero;

				var rotation = ScreenHelper.GetRotation(_helper.Activity);
				var hinge = _helper.DisplayMask.GetBoundingRectsForRotation(rotation).FirstOrDefault();
				var hingeDp = new Rectangle(PixelsToDp(hinge.Left), PixelsToDp(hinge.Top), PixelsToDp(hinge.Width()), PixelsToDp(hinge.Height()));

				return hingeDp;
			}

			public bool IsLandscape
			{
				get
				{
					if (!_isDuo || _helper == null)
						return false;

					var rotation = ScreenHelper.GetRotation(_helper.Activity);

					return (rotation == SurfaceOrientation.Rotation270 || rotation == SurfaceOrientation.Rotation90);
				}
			}

			double PixelsToDp(double px)
				=> px / MainActivity.Resources.DisplayMetrics.Density;


			public Point? GetLocationOnScreen(VisualElement visualElement)
			{
				var view = Platform.Android.Platform.GetRenderer(visualElement);

				if (view?.View == null)
					return null;

				int[] location = new int[2];
				view.View.GetLocationOnScreen(location);
				return new Point(view.View.Context.FromPixels(location[0]), view.View.Context.FromPixels(location[1]));
			}

			public object WatchForChangesOnLayout(VisualElement visualElement, Action action)
			{
				if (action == null)
					return null;

				var view = Platform.Android.Platform.GetRenderer(visualElement);
				var androidView = view?.View;

				if (androidView == null || !androidView.IsAlive())
					return null;

				ViewTreeObserver.IOnGlobalLayoutListener listener = null;
				listener = new GenericGlobalLayoutListener(() =>
				{
					if (!androidView.IsAlive())
					{
						action = null;
						androidView = null;
						try
						{
							_mainActivity?.Window?.DecorView?.RootView?.ViewTreeObserver?.RemoveOnGlobalLayoutListener(listener);
						}
						catch
						{
							// just in case something along the call path here is disposed of
						}

						return;
					}

					action?.Invoke();
				});

				androidView.ViewTreeObserver.AddOnGlobalLayoutListener(listener);
				return listener;
			}

			public void StopWatchingForChangesOnLayout(VisualElement visualElement, object handle)
			{
				if (handle == null)
					return;

				var view = Platform.Android.Platform.GetRenderer(visualElement);
				if (view?.View == null)
					return;

				if (handle is ViewTreeObserver.IOnGlobalLayoutListener vto)
					view.View.ViewTreeObserver.RemoveOnGlobalLayoutListener(vto);
			}
		}
	}
}