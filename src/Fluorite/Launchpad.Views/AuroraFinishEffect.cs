using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Launchpad.Views;

public sealed class AuroraFinishEffect : ShaderEffect
{
	private static readonly PixelShader Shader = CreateShader();

	public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(AuroraFinishEffect), 0, SamplingMode.NearestNeighbor);

	public static readonly DependencyProperty PixelSizeProperty = DependencyProperty.Register("PixelSize", typeof(Point), typeof(AuroraFinishEffect), new UIPropertyMetadata(new Point(1920.0, 1080.0), ShaderEffect.PixelShaderConstantCallback(0)));

	public Brush Input
	{
		get
		{
			return (Brush)GetValue(InputProperty);
		}
		set
		{
			SetValue(InputProperty, value);
		}
	}

	public Point PixelSize
	{
		get
		{
			return (Point)GetValue(PixelSizeProperty);
		}
		set
		{
			SetValue(PixelSizeProperty, value);
		}
	}

	private static PixelShader CreateShader()
	{
		PixelShader pixelShader = new PixelShader();
		pixelShader.UriSource = new Uri("/Fluorite;component/Assets/Shaders/AuroraFinish.ps", UriKind.Relative);
		pixelShader.Freeze();
		return pixelShader;
	}

	public AuroraFinishEffect()
	{
		PixelShader = Shader;
		UpdateShaderValue(InputProperty);
		UpdateShaderValue(PixelSizeProperty);
	}
}
