using System;
using System.Collections.Generic;
using CapnoAnalyzer.Models.Patient;

namespace CapnoAnalyzer.Services
{
    /// <summary>
    /// Hasta verileri için soyut servis arayüzü.
    /// Unit test'lerde mock edilebilir.
    /// </summary>
    public interface IPatientService
    {
        List<Patient> GetAllPatients();
        void SavePatient(Patient patient);
        bool DeletePatient(Guid id);

        void ExportToExcel(List<Patient> patients, string filePath);
        List<Patient> ImportFromExcel(string filePath);
    }
}

