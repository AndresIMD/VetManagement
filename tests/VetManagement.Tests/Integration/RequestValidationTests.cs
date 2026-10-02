using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using VetManagement.Api.Authorization;

namespace VetManagement.Tests.Integration;

/// <summary>
/// Server-side validation mirrors what the staff UI already requires, so the API can't be used to
/// store orders/visits without a patient. Each test has a valid request as a positive control.
/// </summary>
public class RequestValidationTests : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _client;

    public RequestValidationTests(CustomWebAppFactory factory) =>
        _client = factory.CreateClientWithPermissions(
            Permissions.EXAMS_PERFORMED.READ, Permissions.EXAMS_PERFORMED.CREATE, Permissions.MEDICAL.READ, Permissions.MEDICAL.CREATE);

    private static object ExamOrder(int patientId, string responsible, int examId) =>
        new { PatientId = patientId, Responsible = responsible, Items = new[] { new { ExamId = examId } } };

    private static object Visit(int patientId, string patientName) =>
        new { PatientId = patientId, PatientName = patientName, TotalValue = 1000 };

    [Fact]
    public async Task ExamOrder_Valid_IsAccepted()
        => (await _client.PostAsJsonAsync("/api/ExamsPerformed", ExamOrder(7, "dr.vet", 4))).StatusCode.Should().Be(HttpStatusCode.OK);

    [Theory]
    [InlineData(0, "dr.vet", 4)]  // no patient
    [InlineData(7, "", 4)]        // no responsible
    [InlineData(7, "dr.vet", 0)]  // item without exam
    public async Task ExamOrder_Invalid_IsRejected(int patientId, string responsible, int examId)
        => (await _client.PostAsJsonAsync("/api/ExamsPerformed", ExamOrder(patientId, responsible, examId))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

    [Fact]
    public async Task Visit_Valid_IsAccepted()
        => (await _client.PostAsJsonAsync("/api/medical-visits", Visit(7, "Luna"))).StatusCode.Should().Be(HttpStatusCode.OK);

    [Theory]
    [InlineData(0, "Luna")]  // no patient
    [InlineData(7, "")]      // no patient name
    public async Task Visit_Invalid_IsRejected(int patientId, string patientName)
        => (await _client.PostAsJsonAsync("/api/medical-visits", Visit(patientId, patientName))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
