using FluentAssertions;
using VetManagement.Domain.Enums;

namespace VetManagement.Tests.Application;

/// <summary>The exam form lists sample types and containers from these orders: a new value must be added to them too.</summary>
public class ExamSampleOrderTests
{
    [Fact]
    public void Orders_ListEveryValueOnce_ExceptNone()
    {
        ExamSampleOrder.SampleTypes.Should().BeEquivalentTo(Enum.GetValues<SampleType>().Where(v => v != SampleType.None)).And.OnlyHaveUniqueItems();
        ExamSampleOrder.Containers.Should().BeEquivalentTo(Enum.GetValues<SampleContainer>().Where(v => v != SampleContainer.None)).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public void StoredNumbers_OfTheFirstValues_DontChange()
    {
        ((int)SampleType.Other).Should().Be(6);
        ((int)SampleContainer.Other).Should().Be(6);
        ((int)SampleType.Plasma).Should().Be(7);
        ((int)SampleContainer.LavenderTube).Should().Be(11);
    }
}
