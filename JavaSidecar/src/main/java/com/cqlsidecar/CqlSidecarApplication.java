package com.cqlsidecar;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.context.properties.EnableConfigurationProperties;

@SpringBootApplication
@EnableConfigurationProperties(CqlProperties.class)
public class CqlSidecarApplication {
    public static void main(String[] args) {
        SpringApplication.run(CqlSidecarApplication.class, args);
    }
}
