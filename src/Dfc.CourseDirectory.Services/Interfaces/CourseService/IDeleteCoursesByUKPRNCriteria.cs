﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.CourseDirectory.Services.Interfaces.CourseService
{
    public interface IDeleteCoursesByUKPRNCriteria
    {
        int? UKPRN { get; set; }
    }
}
