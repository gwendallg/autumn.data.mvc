﻿using System.ComponentModel.DataAnnotations;
using Autumn.Mvc.Data.Annotations;

namespace Autumn.Mvc.Data.EF.Mysql.Samples.Models
{
    [Resource]
    public class Playlist: AbstractEntity
    {
        [MaxLength(120)]
        public string Name { get; set; }
    }
}